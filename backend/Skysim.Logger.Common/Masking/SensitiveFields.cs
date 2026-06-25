namespace Skysim.Logger.Common.Masking;

public class SensitiveFields
{
    private static readonly Lazy<SensitiveFields> _instance = new(() => new SensitiveFields());

    private readonly HashSet<string> _fields = new(StringComparer.OrdinalIgnoreCase)
    {
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
    };

    private SensitiveFields() { }

    public static SensitiveFields Instance { get; } = _instance.Value;

    public IReadOnlySet<string> DenyList => _fields;

    public bool IsSensitive(string fieldName)
    {
        return _fields.Contains(fieldName);
    }
}
