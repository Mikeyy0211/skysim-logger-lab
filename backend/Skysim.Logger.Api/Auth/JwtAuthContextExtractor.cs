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

    private static readonly string[] ValidAlgorithms =
    [
        SecurityAlgorithms.HmacSha512,
        SecurityAlgorithms.HmacSha256
    ];

    private static readonly string[] UserIdClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        JwtRegisteredClaimNames.Sub,
        "sub",
        "Id",
        "id",
        "userId",
        "UserId",
        "user_id"
    ];

    private static readonly string[] EmailClaimTypes =
    [
        ClaimTypes.Email,
        JwtRegisteredClaimNames.Email,
        "email",
        "Email"
    ];

    private static readonly string[] UsernameClaimTypes =
    [
        "preferred_username",
        "username",
        "unique_name",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
        "name",
        "UserName"
    ];

    private static readonly string[] PartnerIdClaimTypes =
    [
        "PartnerId",
        "partner_id",
        "partnerId"
    ];

    private static readonly string[] RoleClaimTypes =
    [
        ClaimTypes.Role,
        "role",
        "roles"
    ];

    private readonly ConsumerJwtOptions _options;
    private readonly ILogger<JwtAuthContextExtractor> _logger;

    public JwtAuthContextExtractor(
        IOptions<ConsumerJwtOptions> options,
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
        message.PartnerId = null;
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

            _logger.LogWarning(
                "JWT validation skipped: ConsumerJwt config is missing. Issuer={Issuer}, Audience={Audience}, HasKey={HasKey}",
                _options.Issuer,
                _options.Audience,
                !string.IsNullOrWhiteSpace(_options.Key));

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
        message.Username = FirstNonEmptyClaim(principal, UsernameClaimTypes);
        message.UserEmail = FirstNonEmptyClaim(principal, EmailClaimTypes);
        message.PartnerId = FirstNonEmptyClaim(principal, PartnerIdClaimTypes);
        message.Roles = ExtractRoles(principal);
        message.AuthResult = "AUTHENTICATED";
    }

    private ClaimsPrincipal? TryValidateJwt(string token, out string error)
    {
        var candidateKeys = GetCandidateSigningKeys(_options.Key).ToList();
        var hasKey = candidateKeys.Count > 0;
        var tokenAlgorithm = TryReadTokenAlgorithm(token);

        if (!hasKey)
        {
            _logger.LogWarning(
                "JWT validation skipped: signing key is missing or invalid. Issuer={Issuer}, Audience={Audience}, HasKey={HasKey}, TokenAlgorithm={TokenAlgorithm}",
                _options.Issuer,
                _options.Audience,
                hasKey,
                tokenAlgorithm ?? "unknown");

            error = "InvalidKey";
            return null;
        }

        var handler = new JwtSecurityTokenHandler();
        Exception? lastException = null;

        foreach (var candidateKey in candidateKeys)
        {
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,

                ValidateAudience = true,
                ValidAudience = _options.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(candidateKey.KeyBytes),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),

                ValidAlgorithms = ValidAlgorithms
            };

            try
            {
                var principal = handler.ValidateToken(token, parameters, out _);

                _logger.LogDebug(
                    "JWT validation succeeded. KeyMode={KeyMode}, Issuer={Issuer}, Audience={Audience}, TokenAlgorithm={TokenAlgorithm}",
                    candidateKey.Mode,
                    _options.Issuer,
                    _options.Audience,
                    tokenAlgorithm ?? "unknown");

                error = string.Empty;
                return principal;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        error = lastException?.GetType().Name ?? "ValidationFailed";

        _logger.LogWarning(
            "JWT validation failed. ExceptionType={ExceptionType}, Issuer={Issuer}, Audience={Audience}, HasKey={HasKey}, TokenAlgorithm={TokenAlgorithm}",
            error,
            _options.Issuer,
            _options.Audience,
            hasKey,
            tokenAlgorithm ?? "unknown");

        return null;
    }

    private static IEnumerable<SigningKeyCandidate> GetCandidateSigningKeys(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            yield break;

        var utf8Bytes = Encoding.UTF8.GetBytes(key);
        if (utf8Bytes.Length > 0)
            yield return new SigningKeyCandidate("Utf8Raw", utf8Bytes);

        byte[]? base64Bytes = null;

        try
        {
            base64Bytes = Convert.FromBase64String(key);
        }
        catch (FormatException)
        {
            // The key is not Base64. Raw UTF8 candidate above is enough.
        }

        if (base64Bytes is { Length: > 0 } && !utf8Bytes.SequenceEqual(base64Bytes))
            yield return new SigningKeyCandidate("Base64Decoded", base64Bytes);
    }

    private static string? TryReadTokenAlgorithm(string token)
    {
        try
        {
            var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return parsed.SignatureAlgorithm;
        }
        catch
        {
            return null;
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

    private sealed record SigningKeyCandidate(string Mode, byte[] KeyBytes);
}