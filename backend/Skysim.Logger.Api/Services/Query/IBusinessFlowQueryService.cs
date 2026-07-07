using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Contracts.DTOs;

namespace Skysim.Logger.Api.Services.Query;

/// <summary>
/// Service for querying business flows grouped by order code.
/// </summary>
public interface IBusinessFlowQueryService
{
    /// <summary>
    /// Retrieves a paginated list of business flows grouped by order code.
    /// Only flows with a non-null, non-empty orderCode are included.
    /// </summary>
    Task<PagedResponse<BusinessFlowSummaryDto>> GetListAsync(
        BusinessFlowListQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves detailed information for a specific business flow by its order code.
    /// Returns all flows and actions associated with the order code.
    /// </summary>
    Task<BusinessFlowDetailDto?> GetByOrderCodeAsync(
        string orderCode,
        CancellationToken ct = default);
}
