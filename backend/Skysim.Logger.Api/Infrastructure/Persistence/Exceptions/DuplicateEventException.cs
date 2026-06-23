namespace Skysim.Logger.Api.Infrastructure.Persistence.Exceptions;

public class DuplicateEventException : Exception
{
    public Guid EventId { get; }

    public DuplicateEventException(Guid eventId)
        : base($"A log action with eventId '{eventId}' already exists.")
    {
        EventId = eventId;
    }

    public DuplicateEventException(Guid eventId, Exception innerException)
        : base($"A log action with eventId '{eventId}' already exists.", innerException)
    {
        EventId = eventId;
    }
}
