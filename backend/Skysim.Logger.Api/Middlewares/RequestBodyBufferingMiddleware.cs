using Microsoft.AspNetCore.Http.Extensions;

namespace Skysim.Logger.Api.Middlewares;

public class RequestBodyBufferingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestBodyBufferingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        await _next(context);
    }
}
