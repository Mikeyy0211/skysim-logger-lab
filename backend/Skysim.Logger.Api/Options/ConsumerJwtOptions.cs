namespace Skysim.Logger.Api.Options;

public class ConsumerJwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    public bool CanValidateWithSigningKey()
        => !string.IsNullOrWhiteSpace(Key)
            && !string.IsNullOrWhiteSpace(Issuer)
            && !string.IsNullOrWhiteSpace(Audience);
}