using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;

namespace Skysim.Logger.Api.Services.Query;

public interface ILogFlowQueryService
{
    Task<PagedResponse<LogFlowSummaryDto>> GetListAsync(
        LogFlowListQuery query,
        CancellationToken ct = default);

    Task<LogFlowDetailDto?> GetByFlowIdAsync(
        string flowId,
        CancellationToken ct = default);
}
