using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Skysim.Logger.Contracts.Constants;

namespace Skysim.Logger.Contracts.Events;

public class LogEventMessage
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
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
    public string FlowType { get; set; } = string.Empty;

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("actionType")]
    public string ActionType { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("checkoutType")]
    public string? CheckoutType { get; set; }

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

    [JsonPropertyName("orderCode")]
    public string? OrderCode { get; set; }

    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("requestTime")]
    public DateTime? RequestTime { get; set; }

    [JsonPropertyName("responseTime")]
    public DateTime? ResponseTime { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("durationMs")]
    public int? DurationMs { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("queryString")]
    public string? QueryString { get; set; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }

    [JsonPropertyName("requestBody")]
    public string? RequestBody { get; set; }

    [JsonPropertyName("responseBody")]
    public string? ResponseBody { get; set; }

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

    [JsonPropertyName("fullUrl")]
    public string? FullUrl { get; set; }

    [JsonPropertyName("clientIp")]
    public string? ClientIp { get; set; }

    [JsonPropertyName("sourceService")]
    public string? SourceService { get; set; }

    [JsonPropertyName("requestHeaders")]
    public Dictionary<string, string>? RequestHeaders { get; set; }

    [JsonPropertyName("responseHeaders")]
    public Dictionary<string, string>? ResponseHeaders { get; set; }

    // ==== Auth context fields ====
    [JsonPropertyName("hasAuthorization")]
    public bool HasAuthorization { get; set; }

    [JsonPropertyName("authScheme")]
    public string? AuthScheme { get; set; }

    [JsonPropertyName("isAuthenticated")]
    public bool IsAuthenticated { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("userEmail")]
    public string? UserEmail { get; set; }

    [JsonPropertyName("partnerId")]
    public string? PartnerId { get; set; }

    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }

    [JsonPropertyName("authResult")]
    public string? AuthResult { get; set; }
}
