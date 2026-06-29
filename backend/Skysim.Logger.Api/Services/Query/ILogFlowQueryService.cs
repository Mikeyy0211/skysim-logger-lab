using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Contracts.DTOs;

namespace Skysim.Logger.Api.Services.Query;

/// <summary>
/// Service for querying log flow data from the database.
/// </summary>
public interface ILogFlowQueryService
{
    /// <summary>
    /// Retrieves a paginated list of log flows with optional filtering and sorting.
    /// </summary>
    /// <param name="query">Query parameters including filters, sorting, and pagination options.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A paginated response containing log flow summaries.</returns>
    Task<PagedResponse<LogFlowSummaryDto>> GetListAsync(
        LogFlowListQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves detailed information for a specific log flow by its flow ID.
    /// </summary>
    /// <param name="flowId">The unique identifier of the flow.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The log flow details with associated actions, or null if not found.</returns>
    Task<LogFlowDetailDto?> GetByFlowIdAsync(
        string flowId,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a flow with the specified flow ID exists.
    /// </summary>
    /// <param name="flowId">The unique identifier of the flow.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>True if the flow exists; otherwise, false.</returns>
    Task<bool> FlowExistsAsync(
        string flowId,
        CancellationToken ct = default);
}
