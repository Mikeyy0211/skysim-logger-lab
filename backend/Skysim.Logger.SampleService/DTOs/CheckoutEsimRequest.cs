using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Skysim.Logger.SampleService.DTOs;

/// <summary>
/// Request model for eSIM checkout operations.
/// </summary>
public class CheckoutEsimRequest
{
    /// <summary>
    /// The customer's email address for order confirmation and communication.
    /// </summary>
    [JsonPropertyName("customerEmail")]
    [Required]
    [EmailAddress]
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// The customer's phone number for SMS notifications.
    /// </summary>
    [JsonPropertyName("customerPhone")]
    [Required]
    public string CustomerPhone { get; set; } = string.Empty;

    /// <summary>
    /// The eSIM package code to purchase.
    /// </summary>
    [JsonPropertyName("packageCode")]
    [Required]
    public string PackageCode { get; set; } = string.Empty;

    /// <summary>
    /// The quantity of eSIM packages to purchase.
    /// </summary>
    /// <value>Must be between 1 and 100. Defaults to 1.</value>
    [JsonPropertyName("quantity")]
    [Range(1, 100)]
    public int Quantity { get; set; } = 1;
}
