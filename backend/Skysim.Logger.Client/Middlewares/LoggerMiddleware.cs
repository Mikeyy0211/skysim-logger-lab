using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skysim.Logger.Client.Producers;
using Skysim.Logger.Contracts.Events;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;

namespace Skysim.Logger.Client.Middlewares;

/// <summary>
/// HTTP middleware to log request/response to Kafka for UAT integration.
/// </summary>
public class LoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IKafkaLogProducer _producer;
    private readonly ILogger<LoggerMiddleware> _logger;
    private readonly string _serviceName;

    public LoggerMiddleware(
        RequestDelegate next,
        IKafkaLogProducer producer,
        ILogger<LoggerMiddleware> logger,
        IOptions<LoggerMiddlewareOptions> options)
    {
        _next = next;
        _producer = producer;
        _logger = logger;
        _serviceName = options.Value.ServiceName;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip infrastructure paths
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.StartsWith("/swagger") || path.StartsWith("/health") || path.StartsWith("/favicon"))
        {
            await _next(context);
            return;
        }

        // ==== 1. Metadata: flowId + timestamp ====
        var startedAt = DateTime.UtcNow;
        var flowId = GetFlowId(context);

        // ==== 2. Read HTTP context (request phase) ====
        var fullUrl = BuildFullUrl(context.Request);
        var clientIp = GetClientIp(context);
        var sourceService = GetSourceService(context);
        var requestHeaders = GetRequestHeaders(context.Request);

        // ==== 3. Buffer request body ====
        var requestBody = await CaptureRequestBodyAsync(context.Request);

        // ==== 4. Execute + capture response ====
        var (statusCode, responseBody, exception) = await ExecuteAndCaptureResponseAsync(context);

        // ==== 5. Read response headers (after response is ready) ====
        var responseHeaders = GetResponseHeaders(context.Response);

        // ==== 6. Build and publish event ====
        var durationMs = (int)(DateTime.UtcNow - startedAt).TotalMilliseconds;

        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowTypes.HttpAction,
            ActionType = ActionTypes.HttpRequest,
            Status = IsSuccess(statusCode, exception) ? StatusTypes.Success : StatusTypes.Failed,
            ServiceName = _serviceName,
            SourceService = sourceService,
            CreatedAt = startedAt,
            Method = context.Request.Method,
            Path = context.Request.Path.Value,
            QueryString = context.Request.QueryString.Value,
            FullUrl = fullUrl,
            StatusCode = statusCode,
            DurationMs = durationMs,
            ClientIp = clientIp,
            RequestHeaders = requestHeaders,
            ResponseHeaders = responseHeaders,
            RequestBody = requestBody,
            ResponseBody = responseBody,
            ErrorCode = exception?.GetType().Name,
            ErrorMessage = exception?.Message,
            Message = $"{context.Request.Method} {fullUrl} -> {statusCode} ({durationMs}ms)"
        };

        await PublishLogAsync(message);

        if (exception != null)
            throw exception;
    }

    // ==== Private helpers ====

    private static string GetFlowId(HttpContext context)
    {
        var headers = context.Request.Headers;

        if (headers.TryGetValue("X-Flow-Id", out var v) && !string.IsNullOrWhiteSpace(v))
            return v.ToString();
        if (headers.TryGetValue("X-Correlation-Id", out v) && !string.IsNullOrWhiteSpace(v))
            return v.ToString();
        if (headers.TryGetValue("X-Request-Id", out v) && !string.IsNullOrWhiteSpace(v))
            return v.ToString();

        var generated = Guid.NewGuid().ToString("D");
        context.Response.Headers["X-Flow-Id"] = generated;
        return generated;
    }

    private static string BuildFullUrl(HttpRequest request)
    {
        var scheme = request.Scheme;
        var host = request.Host.Value ?? "";
        var path = request.Path.Value ?? "";
        var queryString = request.QueryString.Value ?? "";
        return $"{scheme}://{host}{path}{queryString}";
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string? GetSourceService(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Source-Service", out var v) && !string.IsNullOrWhiteSpace(v))
            return v.ToString();

        if (context.Request.Headers.TryGetValue("X-Caller-Service", out v) && !string.IsNullOrWhiteSpace(v))
            return v.ToString();

        return null;
    }

    private static Dictionary<string, string> GetRequestHeaders(HttpRequest request)
    {
        var headers = new Dictionary<string, string>();
        foreach (var header in request.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }
        return headers;
    }

    private static Dictionary<string, string> GetResponseHeaders(HttpResponse response)
    {
        var headers = new Dictionary<string, string>();
        foreach (var header in response.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }
        return headers;
    }

    private static async Task<string?> CaptureRequestBodyAsync(HttpRequest request)
    {
        if (!request.ContentLength.HasValue || request.ContentLength == 0)
            return null;

        request.EnableBuffering();
        request.Body.Position = 0;

        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        request.Body.Position = 0;

        return ms.Length > 32 * 1024 ? null : Encoding.UTF8.GetString(ms.ToArray());
    }

    private async Task<(int statusCode, string? body, Exception? exception)> ExecuteAndCaptureResponseAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        Exception? exception = null;
        int statusCode;

        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        catch (Exception ex)
        {
            exception = ex;
            statusCode = StatusCodes.Status500InternalServerError;
        }
        finally
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        return buffer.Length > 32 * 1024
            ? (statusCode, null, exception)
            : (statusCode, await new StreamReader(buffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync(), exception);
    }

    private static bool IsSuccess(int statusCode, Exception? exception) =>
        exception == null && statusCode >= 200 && statusCode < 300;

    private async Task PublishLogAsync(LogEventMessage message)
    {
        try
        {
            await _producer.PublishAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka publish failed. EventId={EventId} FlowId={FlowId}",
                message.EventId, message.FlowId);
        }
    }
}
