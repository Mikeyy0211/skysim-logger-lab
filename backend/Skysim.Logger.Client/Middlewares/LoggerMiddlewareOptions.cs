namespace Skysim.Logger.Client.Middlewares;

/// <summary>
/// Options for LoggerMiddleware configuration.
/// </summary>
public class LoggerMiddlewareOptions
{
    public const string SectionName = "Logger";

    /// <summary>
    /// The name of the service that will be used when logging HTTP requests.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
}
