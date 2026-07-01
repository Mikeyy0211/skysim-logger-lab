using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Contracts.Constants;
using Skysim.Logger.SampleService.DTOs;
using Skysim.Logger.SampleService.Services;

namespace Skysim.Logger.SampleService.Controllers;

/// <summary>
/// API controller for eSIM checkout operations.
/// Handles checkout requests and publishes business action logs to Kafka.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private const string FlowIdHeader = "X-Flow-Id";

    private readonly IBusinessActionLogger _businessActionLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutController"/> class.
    /// </summary>
    /// <param name="businessActionLogger">Service for publishing business action logs.</param>
    public CheckoutController(IBusinessActionLogger businessActionLogger)
    {
        _businessActionLogger = businessActionLogger;
    }

    /// <summary>
    /// Processes an eSIM checkout request.
    /// Determines checkout type (Guest or Authenticated) based on Authorization header.
    /// Publishes a complete checkout flow with all business action events to Kafka.
    /// </summary>
    /// <param name="request">The checkout request containing customer and package details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Checkout response with flow ID, order ID, and payment ID.</returns>
    [HttpPost("esim")]
    public async Task<ActionResult<CheckoutEsimResponse>> CheckoutEsim([FromBody] CheckoutEsimRequest request, CancellationToken ct)
    {
        var flowId = GetFlowId();
        var checkoutType = DetermineCheckoutType();
        var orderId = $"ORD-{Guid.NewGuid():N}";
        var paymentId = $"PAY-{Guid.NewGuid():N}";

        var response = new CheckoutEsimResponse
        {
            FlowId = flowId,
            OrderId = orderId,
            PaymentId = paymentId,
            CheckoutType = checkoutType,
            Status = StatusTypes.Success,
            Message = "eSIM checkout request received successfully."
        };

        await _businessActionLogger.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId, ct);

        return Ok(response);
    }

    private string GetFlowId()
    {
        if (Request.Headers.TryGetValue(FlowIdHeader, out var flowId)
            && !string.IsNullOrWhiteSpace(flowId))
        {
            return flowId.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }

    private string DetermineCheckoutType()
    {
        if (Request.Headers.ContainsKey("Authorization"))
        {
            return CheckoutTypes.Authenticated;
        }

        return CheckoutTypes.Guest;
    }
}
