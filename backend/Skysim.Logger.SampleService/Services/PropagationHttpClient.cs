using System.Net.Http.Json;

namespace Skysim.Logger.SampleService.Services;

/// <summary>
/// Typed HttpClient for downstream service calls with flow-context propagation.
/// Configured with FlowContextForwardingHandler in Program.cs.
/// </summary>
public class PropagationHttpClient
{
    private readonly HttpClient _http;

    public PropagationHttpClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Forwards the current X-Flow-Id/X-Correlation-Id to the downstream service.
    /// </summary>
    public async Task<PropagationResponse> CallDownstreamAsync(string downstreamUrl, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(downstreamUrl, ct);

        var flowId = default(string);
        var correlationId = default(string);

        if (response.Headers.TryGetValues("X-Flow-Id", out var flowIdValues))
            flowId = flowIdValues.FirstOrDefault();

        if (response.Headers.TryGetValues("X-Correlation-Id", out var corrIdValues))
            correlationId = corrIdValues.FirstOrDefault();

        return new PropagationResponse
        {
            DownstreamStatusCode = (int)response.StatusCode,
            ReceivedFlowId = flowId,
            ReceivedCorrelationId = correlationId
        };
    }
}

public class PropagationResponse
{
    public int DownstreamStatusCode { get; set; }
    public string? ReceivedFlowId { get; set; }
    public string? ReceivedCorrelationId { get; set; }
}
