namespace Skysim.Logger.Client.Middlewares;

public class LoggerMiddlewareOptions
{
    public const string SectionName = "LoggerMiddleware";

    public string ServiceName { get; set; } = "unknown";

    // JWT verification options used by LoggerMiddleware as a fallback to HttpContext.User.
    // They are read from configuration / environment variables. Never commit a real JwtKey.
    public string JwtKey { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = string.Empty;
    public string JwtAudience { get; set; } = string.Empty;
    public string JwtSubject { get; set; } = string.Empty;
}
