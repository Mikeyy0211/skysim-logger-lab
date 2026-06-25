using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Infrastructure.Repositories;

public interface ILogActionDetailRepository
{
    Task<LogActionDetail> UpsertAsync(LogActionDetail detail, CancellationToken cancellationToken = default);
    Task<LogActionDetail?> GetByActionIdAsync(Guid actionId, CancellationToken cancellationToken = default);
}
