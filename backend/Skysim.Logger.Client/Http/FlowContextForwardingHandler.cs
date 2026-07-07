using System.Net.Http;
using Microsoft.AspNetCore.Http;
using HeaderNames = Skysim.Logger.Contracts.Constants.HeaderNames;

namespace Skysim.Logger.Client.Http;

/// <summary>
/// DelegatingHandler that propagates X-Flow-Id and X-Correlation-Id from the
/// current HttpContext to outbound HTTP requests.
///
/// Usage:
/// <code>
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
            var requestHeaders = httpContext.Request.Headers;

            // Forward X-Flow-Id if present in the current request and not already set on outbound request
            if (requestHeaders.TryGetValue(HeaderNames.FlowId, out var flowId) &&
                !string.IsNullOrWhiteSpace(flowId) &&
                !request.Headers.Contains(HeaderNames.FlowId))
            {
                request.Headers.TryAddWithoutValidation(HeaderNames.FlowId, flowId.ToArray());
            }

            // Forward X-Correlation-Id if present in the current request and not already set on outbound request
            if (requestHeaders.TryGetValue(HeaderNames.CorrelationId, out var corrId) &&
                !string.IsNullOrWhiteSpace(corrId) &&
                !request.Headers.Contains(HeaderNames.CorrelationId))
            {
                request.Headers.TryAddWithoutValidation(HeaderNames.CorrelationId, corrId.ToArray());
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
