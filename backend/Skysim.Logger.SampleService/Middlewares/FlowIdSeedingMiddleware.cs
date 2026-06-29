using Microsoft.AspNetCore.Http;

namespace Skysim.Logger.SampleService.Middlewares;

public class FlowIdSeedingMiddleware
{
    private readonly RequestDelegate _next;
    private const string FlowIdHeader = "X-Flow-Id";

    public FlowIdSeedingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

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
