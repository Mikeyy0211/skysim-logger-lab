using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skysim.Logger.Client.Producers;

namespace Skysim.Logger.Client.Middlewares;

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
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        if (path.StartsWith("/swagger") ||
            path.StartsWith("/health") ||
            path.StartsWith("/favicon"))
        {
            await _next(context);
            return;
        }

        var startedAt = DateTime.UtcNow;
        var flowId = GetFlowId(context);
        var requestBody = await CaptureRequestBodyAsync(context.Request);

        var (statusCode, responseBody, exception) = await ExecuteAndCaptureResponseAsync(context);

        var durationMs = (int)(DateTime.UtcNow - startedAt).TotalMilliseconds;

        try
        {
            await _producer.PublishAsync(new
            {
                eventId = Guid.NewGuid(),
                flowId,
                flowType = "HTTP_ACTION",
                actionType = "HTTP_REQUEST",

                status = exception == null && statusCode >= 200 && statusCode < 400
                    ? "SUCCESS"
                    : "FAILED",

                serviceName = _serviceName,
                createdAt = startedAt,

                method = context.Request.Method,
                path = context.Request.Path.Value,
                queryString = context.Request.QueryString.Value,
                fullUrl = BuildFullUrl(context.Request),

                statusCode,
                durationMs,
                clientIp = GetClientIp(context),

                requestHeaders = GetRequestHeaders(context.Request),
                requestBody,
                responseBody,

                errorCode = exception?.GetType().Name,
                errorMessage = exception?.Message,
                message = $"{context.Request.Method} {context.Request.Path} -> {statusCode} ({durationMs}ms)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka publish failed. FlowId={FlowId}", flowId);
        }

        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

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

        if (!context.Response.HasStarted)
            context.Response.Headers["X-Flow-Id"] = generated;

        return generated;
    }

    private static string BuildFullUrl(HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host.Value}{request.Path.Value}{request.QueryString.Value}";
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static Dictionary<string, string> GetRequestHeaders(HttpRequest request)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var h in request.Headers)
            headers[h.Key] = h.Value.ToString();

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

        return ms.Length > 32 * 1024
            ? null
            : Encoding.UTF8.GetString(ms.ToArray());
    }

    private async Task<(int statusCode, string? body, Exception? exception)> ExecuteAndCaptureResponseAsync(
        HttpContext context)
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

        if (buffer.Length > 32 * 1024)
            return (statusCode, null, exception);

        var body = await new StreamReader(buffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();

        return (statusCode, body, exception);
    }
}
