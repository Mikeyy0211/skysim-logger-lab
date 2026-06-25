using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Infrastructure.Repositories;

public interface ILogActionRepository
{
    Task<LogAction> InsertAsync(LogAction action, CancellationToken cancellationToken = default);
    Task<LogAction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LogAction?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
}
