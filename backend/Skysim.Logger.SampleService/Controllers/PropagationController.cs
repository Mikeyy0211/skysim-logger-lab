using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.SampleService.Services;
using HeaderNames = Skysim.Logger.Contracts.Constants.HeaderNames;

namespace Skysim.Logger.SampleService.Controllers;

/// <summary>
/// Demonstrates flow propagation across service boundaries.
/// All three endpoints share the same X-Flow-Id when called sequentially.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PropagationController : ControllerBase
{
    private readonly PropagationHttpClient _httpClient;
    private readonly ILogger<PropagationController> _logger;

    public PropagationController(PropagationHttpClient httpClient, ILogger<PropagationController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Entry point A: receives a request and calls endpoint B internally.
    /// Returns the flow ID and downstream response for verification.
    /// </summary>
    [HttpGet("start")]
    public async Task<IActionResult> StartPropagation([FromQuery] string downstreamUrl, CancellationToken ct)
    {
        var flowId = GetFlowId();
        _logger.LogInformation("Propagation start. FlowId={FlowId}, DownstreamUrl={Url}", flowId, downstreamUrl);

        PropagationResponse? downstream = null;
        if (!string.IsNullOrWhiteSpace(downstreamUrl))
        {
            downstream = await _httpClient.CallDownstreamAsync(downstreamUrl, ct);
            _logger.LogInformation(
                "Propagation received downstream response. FlowId={FlowId}, DownstreamFlowId={DownstreamFlowId}",
                flowId,
                downstream.ReceivedFlowId);
        }

        return Ok(new
        {
            entryFlowId = flowId,
            downstreamFlowId = downstream?.ReceivedFlowId,
            flowIdsMatch = flowId == downstream?.ReceivedFlowId,
            downstreamResponse = downstream
        });
    }

    /// <summary>
    /// Endpoint B: called by StartPropagation. Logs the incoming request and echoes flow IDs.
    /// </summary>
    [HttpGet("mid")]
    public IActionResult MidPropagation()
    {
        var flowId = GetFlowId();
        _logger.LogInformation("Propagation mid point. FlowId={FlowId}", flowId);
        return Ok(new { receivedFlowId = flowId });
    }

    /// <summary>
    /// Echo endpoint: returns the X-Flow-Id and X-Correlation-Id from the request headers.
    /// Useful for testing propagation manually with curl.
    /// </summary>
    [HttpGet("echo")]
    public IActionResult Echo()
    {
        var flowId = GetFlowId();
        var corrId = GetCorrelationId();
        return Ok(new
        {
            flowId,
            correlationId = corrId,
            note = "Echo endpoint for manual flow propagation testing"
        });
    }

    private string GetFlowId()
    {
        if (Request.Headers.TryGetValue(HeaderNames.FlowId, out var flowId) &&
            !string.IsNullOrWhiteSpace(flowId))
        {
            return flowId.ToString();
        }
        return Guid.NewGuid().ToString("D");
    }

    private string GetCorrelationId()
    {
        if (Request.Headers.TryGetValue(HeaderNames.CorrelationId, out var corrId) &&
            !string.IsNullOrWhiteSpace(corrId))
        {
            return corrId.ToString();
        }
        return GetFlowId();
    }
}
