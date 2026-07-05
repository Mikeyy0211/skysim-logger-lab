using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Skysim.Logger.Api.Auth;
using Skysim.Logger.Api.Options;
using Xunit;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;

namespace Skysim.Logger.Api.Tests.Auth;

public class JwtAuthContextExtractorTests
{
    private const string SigningKey = "test-secret-signing-key-32-bytes-long!";
    private const string IssuerValue = "test-issuer";
    private const string AudienceValue = "test-audience";

    [Fact]
    public void Extract_NoAuthorizationHeader_SetsHasAuthorizationFalseAndNoToken()
    {
        var extractor = CreateExtractor();
        var message = new LogEventMessage { RequestHeaders = new() };

        extractor.Extract(message);

        message.HasAuthorization.Should().BeFalse();
        message.AuthScheme.Should().BeNull();
        message.IsAuthenticated.Should().BeFalse();
        message.UserId.Should().BeNull();
        message.AuthResult.Should().Be("NO_TOKEN");
    }

    [Fact]
    public void Extract_BearerTokenPresent_WithoutJwtConfig_SetsTokenPresentNotAuthenticated()
    {
        var extractor = CreateExtractor(configureOptions: null);
        var message = new LogEventMessage
        {
            RequestHeaders = new() { ["Authorization"] = "Bearer some.token.here" }
        };

        extractor.Extract(message);

        message.HasAuthorization.Should().BeTrue();
        message.AuthScheme.Should().Be("Bearer");
        message.AuthResult.Should().Be("TOKEN_PRESENT_NOT_AUTHENTICATED");
        message.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void Extract_BasicAuthHeader_SetsSchemeToBasic()
    {
        var extractor = CreateExtractor();
        var message = new LogEventMessage
        {
            RequestHeaders = new() { ["Authorization"] = "Basic dXNlcjpwYXNz" }
        };

        extractor.Extract(message);

        message.HasAuthorization.Should().BeTrue();
        message.AuthScheme.Should().Be("Basic");
        message.AuthResult.Should().Be("TOKEN_PRESENT_NOT_AUTHENTICATED");
        message.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void Extract_ValidJwt_ExtractsUserIdEmailUsernameRoles()
    {
        var token = CreateJwt(new[]
        {
            new Claim("sub", "user-123"),
            new Claim("preferred_username", "johndoe"),
            new Claim(ClaimTypes.Email, "john@example.com"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "User")
        });

        var extractor = CreateExtractor();
        var message = new LogEventMessage
        {
            RequestHeaders = new() { ["Authorization"] = $"Bearer {token}" }
        };

        extractor.Extract(message);

        message.IsAuthenticated.Should().BeTrue();
        message.AuthResult.Should().Be("AUTHENTICATED");
        message.UserId.Should().Be("user-123");
        message.Username.Should().Be("johndoe");
        message.UserEmail.Should().Be("john@example.com");
        message.Roles.Should().NotBeNull();
        message.Roles!.Should().Contain("Admin");
        message.Roles.Should().Contain("User");
    }

    [Fact]
    public void Extract_ExpiredJwt_SetsTokenPresentNotAuthenticated()
    {
        var now = DateTime.UtcNow;
        var token = CreateJwt(
            new[] { new Claim("sub", "user-1") },
            notBefore: now.AddMinutes(-10),
            expiresAt: now.AddMinutes(-5));

        var extractor = CreateExtractor();
        var message = new LogEventMessage
        {
            RequestHeaders = new() { ["Authorization"] = $"Bearer {token}" }
        };

        extractor.Extract(message);

        message.IsAuthenticated.Should().BeFalse();
        message.AuthResult.Should().Be("TOKEN_PRESENT_NOT_AUTHENTICATED");
    }

    [Fact]
    public void Extract_InvalidSignature_SetsTokenPresentNotAuthenticated()
    {
        var token = CreateJwt(
            new[] { new Claim("sub", "user-1") },
            signingKey: "wrong-signing-key-different-secret-32!");

        var extractor = CreateExtractor();
        var message = new LogEventMessage
        {
            RequestHeaders = new() { ["Authorization"] = $"Bearer {token}" }
        };

        extractor.Extract(message);

        message.IsAuthenticated.Should().BeFalse();
        message.AuthResult.Should().Be("TOKEN_PRESENT_NOT_AUTHENTICATED");
    }

    [Fact]
    public void Extract_AuthorizationIsCaseInsensitive()
    {
        var token = CreateJwt(new[] { new Claim("sub", "user-1") });

        var extractor = CreateExtractor();
        var message = new LogEventMessage
        {
            RequestHeaders = new() { ["authorization"] = $"Bearer {token}" }
        };

        extractor.Extract(message);

        message.IsAuthenticated.Should().BeTrue();
        message.UserId.Should().Be("user-1");
    }

    private static JwtAuthContextExtractor CreateExtractor(JwtOptions? configureOptions = null)
    {
        var options = configureOptions ?? new JwtOptions
        {
            Key = SigningKey,
            Issuer = IssuerValue,
            Audience = AudienceValue
        };

        return new JwtAuthContextExtractor(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<JwtAuthContextExtractor>.Instance);
    }

    private static string CreateJwt(
        IEnumerable<Claim> claims,
        DateTime? notBefore = null,
        DateTime? expiresAt = null,
        string? signingKey = null)
    {
        var key = signingKey ?? SigningKey;
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: IssuerValue,
            audience: AudienceValue,
            claims: claims,
            notBefore: notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            expires: expiresAt ?? DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
