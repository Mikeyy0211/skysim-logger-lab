using System.IdentityModel.Tokens.Jwt;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Skysim.Logger.Client.Producers;

namespace Skysim.Logger.Client.Middlewares;

public class LoggerMiddleware
{
    private const string BearerScheme = "Bearer";

    private static readonly string[] UserIdClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        "user_id",
        "userId",
        "UserId",
        "nameid",
        "id"
    ];

    private static readonly string[] RoleClaimTypes =
    [
        ClaimTypes.Role,
        "role",
        "roles"
    ];

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

    private readonly RequestDelegate _next;
    private readonly IKafkaLogProducer _producer;
    private readonly ILogger<LoggerMiddleware> _logger;
    private readonly string _serviceName;
    private readonly TokenValidationParameters? _jwtParameters;

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

        var jwtKey = options.Value.JwtKey ?? string.Empty;
        var jwtIssuer = options.Value.JwtIssuer ?? string.Empty;
        var jwtAudience = options.Value.JwtAudience ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(jwtKey)
            && !string.IsNullOrWhiteSpace(jwtIssuer)
            && !string.IsNullOrWhiteSpace(jwtAudience))
        {
            _jwtParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(GetJwtKeyBytes(jwtKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        }
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

                userId = auth.userId,
                hasAuthorization = auth.hasAuthorization,
                authScheme = auth.authScheme,
                isAuthenticated = auth.isAuthenticated,
                username = auth.username,
                userEmail = auth.email,
                roles = auth.roles,
                authResult = auth.authResult
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

    private (
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
        var token = ExtractBearerToken(authHeader);

        var hasAuthorization = !string.IsNullOrWhiteSpace(authHeader);
        var authScheme = GetAuthScheme(authHeader);

        var user = context.User;
        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;

        string? userId = null;
        string? username = null;
        string? email = null;
        List<string>? roles = null;
        var userAuthenticated = false;

        if (isAuthenticated)
        {
            userAuthenticated = ReadUserFromPrincipal(user, out userId, out username, out email, out roles);
        }

        var tokenValidated = false;
        if (!userAuthenticated && token != null)
        {
            tokenValidated = TryValidateJwt(token, out var jwtUserId, out var jwtUsername, out var jwtEmail, out var jwtRoles);
            if (tokenValidated)
            {
                userId ??= jwtUserId;
                username ??= jwtUsername;
                email ??= jwtEmail;
                roles ??= jwtRoles;
            }
        }

        var authResult = ResolveAuthResult(statusCode, userAuthenticated, hasAuthorization, tokenValidated);

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

    private static bool ReadUserFromPrincipal(
        ClaimsPrincipal user,
        out string? userId,
        out string? username,
        out string? email,
        out List<string>? roles)
    {
        userId = FirstNonEmptyClaim(user, UserIdClaimTypes);

        username = user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue("username")
            ?? user.Identity?.Name;

        email = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        roles = ExtractRoles(user);
        if (roles != null && roles.Count == 0)
            roles = null;

        return !string.IsNullOrEmpty(userId);
    }

    private static string? FirstNonEmptyClaim(ClaimsPrincipal user, IReadOnlyList<string> claimTypes)
    {
        foreach (var type in claimTypes)
        {
            var value = user.FindFirstValue(type);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }

    private static List<string>? ExtractRoles(ClaimsPrincipal user)
    {
        var collected = new List<string>();

        foreach (var claim in user.Claims)
        {
            if (!IsRoleClaimType(claim.Type) || string.IsNullOrWhiteSpace(claim.Value))
                continue;

            foreach (var part in claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && !collected.Contains(trimmed))
                    collected.Add(trimmed);
            }
        }

        return collected.Count > 0 ? collected : null;
    }

    private static bool IsRoleClaimType(string claimType)
    {
        foreach (var roleType in RoleClaimTypes)
        {
            if (claimType.Equals(roleType, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private bool TryValidateJwt(
        string token,
        out string? userId,
        out string? username,
        out string? email,
        out List<string>? roles)
    {
        userId = null;
        username = null;
        email = null;
        roles = null;

        if (_jwtParameters == null)
            return false;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, _jwtParameters, out _);

            userId = FirstNonEmptyClaim(principal, UserIdClaimTypes);

            username = principal.FindFirstValue("preferred_username")
                ?? principal.FindFirstValue("username")
                ?? principal.Identity?.Name;

            email = principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue("email");

            roles = ExtractRoles(principal);
            if (roles != null && roles.Count == 0)
                roles = null;

            return true;
        }
        catch (Exception ex) when (ex is SecurityTokenException || ex is ArgumentException || ex is FormatException)
        {
            _logger.LogDebug("JWT validation failed. ExceptionType={ExceptionType}", ex.GetType().Name);
            return false;
        }
    }

    private static string ResolveAuthResult(int statusCode, bool isAuthenticated, bool hasAuthorization, bool tokenValidated)
    {
        if (statusCode == StatusCodes.Status401Unauthorized)
            return "UNAUTHENTICATED";

        if (statusCode == StatusCodes.Status403Forbidden)
            return "FORBIDDEN";

        if (isAuthenticated)
            return "AUTHENTICATED";

        if (tokenValidated)
            return "TOKEN_VALIDATED";

        if (hasAuthorization)
            return "TOKEN_PRESENT_NOT_AUTHENTICATED";

        return "NO_TOKEN";
    }

    private static string? ExtractBearerToken(string? authHeader)
    {
        if (string.IsNullOrWhiteSpace(authHeader))
            return null;

        const string prefix = BearerScheme + " ";
        if (authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return authHeader.Substring(prefix.Length).Trim();

        return null;
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

        if (value.StartsWith(BearerScheme + " ", StringComparison.OrdinalIgnoreCase))
            return "Bearer ***";

        return "***";
    }

    private static byte[] GetJwtKeyBytes(string jwtKey)
    {
        try
        {
            return Convert.FromBase64String(jwtKey);
        }
        catch (FormatException)
        {
            return Encoding.UTF8.GetBytes(jwtKey);
        }
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