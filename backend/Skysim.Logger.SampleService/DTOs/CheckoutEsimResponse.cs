using System.Text.Json.Serialization;

namespace Skysim.Logger.SampleService.DTOs;

public class CheckoutEsimResponse
{
    [JsonPropertyName("flowId")]
    public string FlowId { get; set; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("checkoutType")]
    public string CheckoutType { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
