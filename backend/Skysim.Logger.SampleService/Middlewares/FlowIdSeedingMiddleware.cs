using Microsoft.AspNetCore.Http;

namespace Skysim.Logger.SampleService.Middlewares;

/// <summary>
/// Middleware that ensures every HTTP request has a FlowId.
/// If the incoming request does not contain an X-Flow-Id header, a new UUID is generated.
/// </summary>
public class FlowIdSeedingMiddleware
{
    private readonly RequestDelegate _next;
    private const string FlowIdHeader = "X-Flow-Id";

    public FlowIdSeedingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Processes the HTTP request and seeds a FlowId if not present.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(FlowIdHeader, out var existingFlowId)
            || string.IsNullOrWhiteSpace(existingFlowId))
        {
            var newFlowId = Guid.NewGuid().ToString("N");
            context.Request.Headers[FlowIdHeader] = newFlowId;
        }

        await _next(context);
    }
}

public static class FlowIdSeedingMiddlewareExtensions
{
    public static IApplicationBuilder UseFlowIdSeeding(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<FlowIdSeedingMiddleware>();
    }
}
