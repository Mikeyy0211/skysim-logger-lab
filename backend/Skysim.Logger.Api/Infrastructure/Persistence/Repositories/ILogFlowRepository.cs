using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Domain.Entities;

namespace Skysim.Logger.Api.Infrastructure.Persistence.Repositories;

public interface ILogFlowRepository
{
    Task<LogFlow> UpsertAsync(
        LogEventMessage message,
        CancellationToken cancellationToken = default);
}
