namespace Skysim.Logger.Api.Domain.Entities;

public class LogActionDetail
{
    public Guid Id { get; set; }
    public Guid ActionId { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? ErrorPayload { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public LogAction? Action { get; set; }
}
