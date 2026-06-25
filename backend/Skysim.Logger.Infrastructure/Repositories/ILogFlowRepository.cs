using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Infrastructure.Repositories;

public interface ILogFlowRepository
{
    Task<LogFlow?> GetByFlowIdAsync(string flowId, CancellationToken cancellationToken = default);
    Task<LogFlow> UpsertAsync(string flowId, Action<LogFlow> updateFlow, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string flowId, CancellationToken cancellationToken = default);
}
