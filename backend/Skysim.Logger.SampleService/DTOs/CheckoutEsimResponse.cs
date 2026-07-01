using System.Text.Json.Serialization;

namespace Skysim.Logger.SampleService.DTOs;

/// <summary>
/// Response model for eSIM checkout operations.
/// Contains identifiers and status information for tracking the checkout flow.
/// </summary>
public class CheckoutEsimResponse
{
    /// <summary>
    /// The unique flow identifier for correlating all events in this checkout process.
    /// </summary>
    [JsonPropertyName("flowId")]
    public string FlowId { get; set; } = string.Empty;

    /// <summary>
    /// The generated order identifier.
    /// </summary>
    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// The generated payment identifier.
    /// </summary>
    [JsonPropertyName("paymentId")]
    public string PaymentId { get; set; } = string.Empty;

    /// <summary>
    /// The checkout type (Guest or Authenticated).
    /// </summary>
    [JsonPropertyName("checkoutType")]
    public string CheckoutType { get; set; } = string.Empty;

    /// <summary>
    /// The status of the checkout request.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable message describing the checkout result.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
