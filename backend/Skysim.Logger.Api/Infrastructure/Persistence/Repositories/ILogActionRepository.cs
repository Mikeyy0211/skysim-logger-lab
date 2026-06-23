using Skysim.Logger.Api.Domain.Entities;

namespace Skysim.Logger.Api.Infrastructure.Persistence.Repositories;

public interface ILogActionRepository
{
    Task<LogAction> InsertAsync(
        LogAction action,
        CancellationToken cancellationToken = default);
}
