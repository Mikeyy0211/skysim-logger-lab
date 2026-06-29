using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Contracts.Constants;
using Skysim.Logger.SampleService.DTOs;

namespace Skysim.Logger.SampleService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private const string FlowIdHeader = "X-Flow-Id";

    [HttpPost("esim")]
    public ActionResult<CheckoutEsimResponse> CheckoutEsim([FromBody] CheckoutEsimRequest request)
    {
        var flowId = GetFlowId();
        var checkoutType = DetermineCheckoutType();
        var orderId = $"ORD-{Guid.NewGuid():N}";

        var response = new CheckoutEsimResponse
        {
            FlowId = flowId,
            OrderId = orderId,
            CheckoutType = checkoutType,
            Status = StatusTypes.Success,
            Message = "eSIM checkout request received successfully."
        };

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
