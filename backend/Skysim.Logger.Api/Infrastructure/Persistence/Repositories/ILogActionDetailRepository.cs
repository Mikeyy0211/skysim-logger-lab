using Skysim.Logger.Api.Domain.Entities;

namespace Skysim.Logger.Api.Infrastructure.Persistence.Repositories;

public interface ILogActionDetailRepository
{
    Task<LogActionDetail> UpsertAsync(
        LogActionDetail detail,
        CancellationToken cancellationToken = default);
}
