using Microsoft.AspNetCore.Builder;
using Skysim.Logger.Client.Middlewares;

namespace Skysim.Logger.Client.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSkysimLogger(this IApplicationBuilder app)
    {
        app.UseMiddleware<LoggerMiddleware>();
        return app;
    }
}
