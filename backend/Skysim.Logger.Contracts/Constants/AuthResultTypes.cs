namespace Skysim.Logger.Contracts.Constants;

public static class AuthResultTypes
{
    public const string NoToken = "NO_TOKEN";
    public const string TokenPresentNotAuthenticated = "TOKEN_PRESENT_NOT_AUTHENTICATED";
    public const string Authenticated = "AUTHENTICATED";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
}
