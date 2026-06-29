namespace Skysim.Logger.Common.Middleware;

using System.Text.Json;
using System.Text.Json.Serialization;

public class MiddlewareLogEntry
{
    [JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("requestTime")]
    public DateTime RequestTime { get; set; }

    [JsonPropertyName("responseTime")]
    public DateTime? ResponseTime { get; set; }

    [JsonPropertyName("duration")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("requestData")]
    public string? RequestData { get; set; }

    [JsonPropertyName("responseData")]
    public string? ResponseData { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("exception")]
    public string? Exception { get; set; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }
}

public interface IMiddlewareLogPublisher
{
    Task PublishAsync(MiddlewareLogEntry entry, CancellationToken cancellationToken = default);
}

public static class MiddlewareLogSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static string Serialize(MiddlewareLogEntry entry)
    {
        return JsonSerializer.Serialize(entry, JsonOptions);
    }

    public static MiddlewareLogEntry? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<MiddlewareLogEntry>(json, JsonOptions);
    }
}
