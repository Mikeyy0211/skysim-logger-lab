using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skysim.Logger.Contracts.Events;

public class LogEventMessage
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) }
    };

    public static LogEventMessage? Deserialize(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return null;
        }

        var jsonString = Encoding.UTF8.GetString(payload);
        return JsonSerializer.Deserialize<LogEventMessage>(jsonString, JsonOptions);
    }

    [JsonPropertyName("eventId")]
    public Guid EventId { get; set; }

    [JsonPropertyName("flowId")]
    public string FlowId { get; set; } = string.Empty;

    [JsonPropertyName("flowType")]
    public Constants.FlowType FlowType { get; set; }

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("actionType")]
    public Constants.ActionType ActionType { get; set; }

    [JsonPropertyName("status")]
    public Constants.Status Status { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("checkoutType")]
    public Constants.CheckoutType? CheckoutType { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("customerEmail")]
    public string? CustomerEmail { get; set; }

    [JsonPropertyName("customerPhone")]
    public string? CustomerPhone { get; set; }

    [JsonPropertyName("orderId")]
    public string? OrderId { get; set; }

    [JsonPropertyName("paymentId")]
    public string? PaymentId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("requestTime")]
    public DateTime? RequestTime { get; set; }

    [JsonPropertyName("responseTime")]
    public DateTime? ResponseTime { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("requestData")]
    public JsonElement? RequestData { get; set; }

    [JsonPropertyName("responseData")]
    public JsonElement? ResponseData { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("exception")]
    public string? Exception { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}
