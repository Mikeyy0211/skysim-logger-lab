namespace Skysim.Logger.Api.Options;

public class JwtOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = true;

    // Symmetric signing key used by JwtAuthContextExtractor to validate incoming tokens.
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    public bool CanValidateWithSigningKey()
        => !string.IsNullOrWhiteSpace(Key)
            && !string.IsNullOrWhiteSpace(Issuer)
            && !string.IsNullOrWhiteSpace(Audience);
}
