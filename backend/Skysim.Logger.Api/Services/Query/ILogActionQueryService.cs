using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;

namespace Skysim.Logger.Api.Services.Query;

public interface ILogActionQueryService
{
    Task<PagedResponse<LogActionDto>> GetByFlowIdAsync(
        LogActionListQuery query,
        CancellationToken ct = default);

    Task<LogActionDetailsDto?> GetDetailsAsync(
        Guid actionId,
        CancellationToken ct = default);
}
