using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Skysim.Logger.Client.Middlewares;
using HeaderNames = Skysim.Logger.Contracts.Constants.HeaderNames;

namespace Skysim.Logger.Client.Http;

/// <summary>
/// DelegatingHandler that propagates X-Flow-Id and X-Correlation-Id from the
/// current HttpContext to outbound HTTP requests.
///
/// Resolution priority (per header):
///   1. <c>HttpContext.Items["FlowContext"]</c> set by <see cref="LoggerMiddleware"/>
///      — works even when the inbound request did not include X-Flow-Id (middleware
///      generated one).
///   2. Inbound <c>Request.Headers</c> — useful when the consuming service did not
///      register LoggerMiddleware but still has X-Flow-Id in the request.
///   3. Skipped silently if HttpContext is null (e.g. BackgroundService / host runners).
///
/// Headers are never overwritten on the outbound <c>HttpRequestMessage</c> if the
/// caller already set them.
///
/// Usage:
/// <code>
/// services.AddSkysimLogger(builder.Configuration); // registers this handler in DI
///
/// services.AddHttpClient("PaymentService")
///     .AddHttpMessageHandler&lt;FlowContextForwardingHandler&gt;();
/// </code>
/// </summary>
public class FlowContextForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FlowContextForwardingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            // Preferred source: typed FlowContext stored by LoggerMiddleware.
            // Survives the case where the inbound request had no X-Flow-Id header
            // because the middleware still synthesises one and places it here.
            if (httpContext.Items.TryGetValue(FlowContext.HttpContextItemKey, out var raw)
                && raw is FlowContext flowContext)
            {
                TryAddIfMissing(request, HeaderNames.FlowId, flowContext.FlowId);
                TryAddIfMissing(request, HeaderNames.CorrelationId, flowContext.CorrelationId);
            }
            else
            {
                // Fallback: read directly from inbound headers. Skipped silently if absent.
                CopyHeaderIfPresent(httpContext.Request.Headers, request, HeaderNames.FlowId);
                CopyHeaderIfPresent(httpContext.Request.Headers, request, HeaderNames.CorrelationId);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static void TryAddIfMissing(HttpRequestMessage request, string headerName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (request.Headers.Contains(headerName)) return;
        request.Headers.TryAddWithoutValidation(headerName, value);
    }

    private static void CopyHeaderIfPresent(IHeaderDictionary source, HttpRequestMessage request, string headerName)
    {
        if (request.Headers.Contains(headerName)) return;
        if (source.TryGetValue(headerName, out var values)
            && !string.IsNullOrWhiteSpace(values))
        {
            request.Headers.TryAddWithoutValidation(headerName, values.ToArray());
        }
    }
}
