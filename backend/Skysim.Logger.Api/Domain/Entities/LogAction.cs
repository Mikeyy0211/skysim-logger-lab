namespace Skysim.Logger.Api.Domain.Entities;

public class LogAction
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string FlowId { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? RequestTime { get; set; }
    public DateTime? ResponseTime { get; set; }
    public int? DurationMs { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public LogFlow? Flow { get; set; }
    public LogActionDetail? Detail { get; set; }
}
