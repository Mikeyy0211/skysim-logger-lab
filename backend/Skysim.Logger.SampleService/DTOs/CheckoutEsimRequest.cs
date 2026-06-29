using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Skysim.Logger.SampleService.DTOs;

public class CheckoutEsimRequest
{
    [JsonPropertyName("customerEmail")]
    [Required]
    [EmailAddress]
    public string CustomerEmail { get; set; } = string.Empty;

    [JsonPropertyName("customerPhone")]
    [Required]
    public string CustomerPhone { get; set; } = string.Empty;

    [JsonPropertyName("packageCode")]
    [Required]
    public string PackageCode { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    [Range(1, 100)]
    public int Quantity { get; set; } = 1;
}
