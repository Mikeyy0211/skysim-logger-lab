using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Skysim.Logger.Api.Options;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;

namespace Skysim.Logger.Api.Auth;

public class JwtAuthContextExtractor : IJwtAuthContextExtractor
{
    private const string AuthorizationHeader = "Authorization";
    private const string BearerScheme = "Bearer";

    private static readonly string[] UserIdClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        "user_id",
        "userId",
        "UserId",
        "Id",
        "nameid"
    ];

    private static readonly string[] RoleClaimTypes =
    [
        ClaimTypes.Role,
        "role",
        "roles"
    ];

    private readonly JwtOptions _options;
    private readonly ILogger<JwtAuthContextExtractor> _logger;

    public JwtAuthContextExtractor(
        IOptions<JwtOptions> options,
        ILogger<JwtAuthContextExtractor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void Extract(LogEventMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var headers = message.RequestHeaders ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var authHeader = GetHeader(headers, AuthorizationHeader);
        var token = ExtractBearerToken(authHeader);

        message.HasAuthorization = !string.IsNullOrWhiteSpace(authHeader);
        message.AuthScheme = GetAuthScheme(authHeader);
        message.IsAuthenticated = false;
        message.UserId = null;
        message.Username = null;
        message.UserEmail = null;
        message.Roles = null;

        if (string.IsNullOrWhiteSpace(authHeader))
        {
            message.AuthResult = "NO_TOKEN";
            return;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            message.AuthResult = "TOKEN_PRESENT_NOT_AUTHENTICATED";
            return;
        }

        if (!_options.CanValidateWithSigningKey())
        {
            message.AuthResult = "TOKEN_PRESENT_NOT_AUTHENTICATED";
            return;
        }

        var principal = TryValidateJwt(token, out var error);
        if (principal == null)
        {
            _logger.LogDebug("JWT validation failed. Error={Error}", error);
            message.AuthResult = "TOKEN_PRESENT_NOT_AUTHENTICATED";
            return;
        }

        message.IsAuthenticated = true;
        message.UserId = FirstNonEmptyClaim(principal, UserIdClaimTypes);
        message.Username = principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue("username")
            ?? principal.Identity?.Name;
        message.UserEmail = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email")
            ?? principal.FindFirstValue("Email");
        message.Roles = ExtractRoles(principal);
        message.AuthResult = "AUTHENTICATED";
    }

    private ClaimsPrincipal? TryValidateJwt(string token, out string error)
    {
        var keyBytes = TryDecodeKey(_options.Key);
        if (keyBytes.Length == 0)
        {
            error = "InvalidKey";
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, parameters, out _);
            error = string.Empty;
            return principal;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            return null;
        }
    }

    private static byte[] TryDecodeKey(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return Encoding.UTF8.GetBytes(value);
        }
    }

    private static string? GetHeader(IDictionary<string, string> headers, string key)
    {
        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return null;
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
}
