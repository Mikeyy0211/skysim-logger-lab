using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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

    private static readonly string[] SensitiveFields =
    [
        "password",
        "access_token",
        "refresh_token",
        "authorization",
        "otp",
        "cardNumber",
        "cvv",
        "paymentSecret",
        "secret",
        "token"
    ];

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
        var auth = GetAuthContext(context, statusCode);

        try
        {
            await _producer.PublishAsync(new
            {
                eventId = Guid.NewGuid(),
                flowId,
                flowType = "HTTP_ACTION",
                actionType = "HTTP_REQUEST",
                status = exception == null && statusCode >= 200 && statusCode < 300
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

                requestHeaders = GetMaskedRequestHeaders(context.Request),
                responseHeaders = GetResponseHeaders(context.Response),
                requestBody = MaskSensitiveJson(requestBody),
                responseBody = MaskSensitiveJson(responseBody),

                errorCode = exception?.GetType().Name,
                errorMessage = exception?.Message,
                message = $"{context.Request.Method} {context.Request.Path} -> {statusCode} ({durationMs}ms)",

                auth.userId,
                auth.hasAuthorization,
                auth.authScheme,
                auth.isAuthenticated,
                auth.username,
                auth.email,
                auth.roles,
                auth.authResult
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

    private static Dictionary<string, string> GetMaskedRequestHeaders(HttpRequest request)
    {
        var headers = new Dictionary<string, string>();

        foreach (var h in request.Headers)
        {
            headers[h.Key] = h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                ? MaskAuthHeader(h.Value.ToString())
                : IsSensitive(h.Key)
                    ? "***"
                    : h.Value.ToString();
        }

        return headers;
    }

    private static Dictionary<string, string> GetResponseHeaders(HttpResponse response)
    {
        var headers = new Dictionary<string, string>();

        foreach (var h in response.Headers)
        {
            headers[h.Key] = h.Value.ToString();
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

    private static (
        bool hasAuthorization,
        string? authScheme,
        bool isAuthenticated,
        string? userId,
        string? username,
        string? email,
        List<string>? roles,
        string authResult
    ) GetAuthContext(HttpContext context, int statusCode)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        var hasAuthorization = !string.IsNullOrWhiteSpace(authHeader);
        var authScheme = GetAuthScheme(authHeader);

        var user = context.User;
        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;

        string? userId = null;
        string? username = null;
        string? email = null;
        List<string>? roles = null;

        if (isAuthenticated)
        {
            userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("user_id");

            username = user.FindFirstValue("preferred_username")
                ?? user.Identity?.Name;

            email = user.FindFirstValue(ClaimTypes.Email)
                ?? user.FindFirstValue("email");

            var allRoles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
                .Concat(user.FindAll("role").Select(c => c.Value))
                .Concat(user.FindAll("roles").Select(c => c.Value))
                .Distinct()
                .ToList();

            roles = allRoles.Count > 0 ? allRoles : null;
        }

        var authResult = statusCode switch
        {
            StatusCodes.Status401Unauthorized => "UNAUTHENTICATED",
            StatusCodes.Status403Forbidden => "FORBIDDEN",
            _ when isAuthenticated => "AUTHENTICATED",
            _ when hasAuthorization => "TOKEN_PRESENT_NOT_AUTHENTICATED",
            _ => "NO_TOKEN"
        };

        return (
            hasAuthorization,
            authScheme,
            isAuthenticated,
            userId,
            username,
            email,
            roles,
            authResult
        );
    }

    private static string? GetAuthScheme(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 0 ? parts[0] : null;
    }

    private static string MaskAuthHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return "Bearer ***";

        return "***";
    }

    private static bool IsSensitive(string fieldName)
    {
        return SensitiveFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
    }

    private static string? MaskSensitiveJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return MaskElement(doc.RootElement).GetRawText();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static JsonElement MaskElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => MaskObject(element),
            JsonValueKind.Array => MaskArray(element),
            _ => element
        };
    }

    private static JsonElement MaskObject(JsonElement element)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartObject();

        foreach (var prop in element.EnumerateObject())
        {
            writer.WritePropertyName(prop.Name);

            if (IsSensitive(prop.Name))
                writer.WriteStringValue("***");
            else
                MaskElement(prop.Value).WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        ms.Position = 0;

        using var doc = JsonDocument.Parse(ms);
        return doc.RootElement.Clone();
    }

    private static JsonElement MaskArray(JsonElement element)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartArray();

        foreach (var item in element.EnumerateArray())
        {
            MaskElement(item).WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();

        ms.Position = 0;

        using var doc = JsonDocument.Parse(ms);
        return doc.RootElement.Clone();
    }
}