namespace Skysim.Logger.Api.Domain.Entities;

public class LogFlow
{
    public Guid Id { get; set; }
    public string FlowId { get; set; } = string.Empty;
    public string FlowType { get; set; } = string.Empty;
    public string? CheckoutType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? UserId { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentId { get; set; }
    public int TotalSteps { get; set; }
    public int SuccessSteps { get; set; }
    public int FailedSteps { get; set; }
    public string? LastActionType { get; set; }
    public string? LastMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<LogAction> Actions { get; set; } = new List<LogAction>();
}
