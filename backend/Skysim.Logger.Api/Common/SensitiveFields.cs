namespace Skysim.Logger.Api.Common;

public static class SensitiveFields
{
    private static readonly HashSet<string> Fields = new(StringComparer.OrdinalIgnoreCase)
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

    public static IReadOnlySet<string> DenyList => Fields;

    public static bool IsSensitive(string fieldName)
    {
        return Fields.Contains(fieldName);
    }
}
