namespace Skysim.Logger.Infrastructure.Entities;

public class LogActionDetail : BaseEntity
{
    public Guid ActionId { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public string? ErrorPayload { get; set; }
    public string? Metadata { get; set; }
    public LogAction? Action { get; set; }
}
