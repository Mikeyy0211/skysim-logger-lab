using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skysim.Logger.Client.Producers;
using Skysim.Logger.Contracts.Events;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;
using AuthResultTypes = Skysim.Logger.Contracts.Constants.AuthResultTypes;

namespace Skysim.Logger.Client.Middlewares;

/// <summary>
/// HTTP middleware to log request/response to Kafka for UAT integration.
/// </summary>
public class LoggerMiddleware
{
    private sealed record AuthContext(
        bool HasAuthorization,
        string? AuthScheme,
        bool IsAuthenticated,
        string? UserId,
        string? Username,
        string? UserEmail,
        List<string>? Roles,
        string? AuthResult);
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

        // ==== 5. Read response headers and auth context (after response is ready) ====
        var responseHeaders = GetResponseHeaders(context.Response);
        var authContext = GetAuthContext(context, statusCode);

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
            Message = $"{context.Request.Method} {fullUrl} -> {statusCode} ({durationMs}ms)",
            UserId = authContext.UserId,
            HasAuthorization = authContext.HasAuthorization,
            AuthScheme = authContext.AuthScheme,
            IsAuthenticated = authContext.IsAuthenticated,
            Username = authContext.Username,
            UserEmail = authContext.UserEmail,
            Roles = authContext.Roles,
            AuthResult = authContext.AuthResult
        };

        await PublishLogAsync(message);

        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
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
        if (!context.Response.HasStarted)
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
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                var value = header.Value.ToString();
                headers[header.Key] = MaskAuthorizationHeader(value);
            }
            else
            {
                headers[header.Key] = header.Value.ToString();
            }
        }
        return headers;
    }

    private static string MaskAuthorizationHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return "Bearer ***";

        if (value.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase))
            return "bearer ***";

        return "***";
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

    private static string? GetAuthScheme(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return null;

        var parts = authorizationHeader.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    private static AuthContext GetAuthContext(HttpContext context, int statusCode)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        var hasAuthorization = !string.IsNullOrWhiteSpace(authHeader);
        var authScheme = GetAuthScheme(authHeader);

        var user = context.User;
        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;

        string? userId = null;
        string? username = null;
        string? email = null;
        var roles = new List<string>();

        if (isAuthenticated)
        {
            userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("user_id");

            username = user.FindFirstValue("preferred_username")
                ?? user.Identity?.Name;

            email = user.FindFirstValue(ClaimTypes.Email)
                ?? user.FindFirstValue("email");

            roles.AddRange(user.FindAll(ClaimTypes.Role).Select(c => c.Value));
            roles.AddRange(user.FindAll("role").Select(c => c.Value));
        }

        var authResult = DetermineAuthResult(statusCode, hasAuthorization, isAuthenticated);

        return new AuthContext(
            hasAuthorization,
            authScheme,
            isAuthenticated,
            userId,
            username,
            email,
            roles.Count > 0 ? roles : null,
            authResult);
    }

    private static string DetermineAuthResult(int statusCode, bool hasAuthorization, bool isAuthenticated)
    {
        if (statusCode == StatusCodes.Status401Unauthorized)
            return AuthResultTypes.Unauthenticated;
        if (statusCode == StatusCodes.Status403Forbidden)
            return AuthResultTypes.Forbidden;
        if (isAuthenticated)
            return AuthResultTypes.Authenticated;
        if (hasAuthorization)
            return AuthResultTypes.TokenPresentNotAuthenticated;
        return AuthResultTypes.NoToken;
    }

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
