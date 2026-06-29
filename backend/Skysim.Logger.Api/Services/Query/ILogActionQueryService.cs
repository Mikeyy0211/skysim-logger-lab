using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;
using Skysim.Logger.Contracts.DTOs;

namespace Skysim.Logger.Api.Services.Query;

/// <summary>
/// Service for querying log action data from the database.
/// </summary>
public interface ILogActionQueryService
{
    /// <summary>
    /// Retrieves a paginated list of log actions for a specific flow.
    /// </summary>
    /// <param name="query">Query parameters including flow ID, filters, and pagination options.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A paginated response containing log action summaries.</returns>
    Task<PagedResponse<LogActionDto>> GetByFlowIdAsync(
        LogActionListQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves detailed information for a specific log action by its action ID.
    /// Includes masked request/response payloads for security.
    /// </summary>
    /// <param name="actionId">The unique identifier of the action.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The log action details with masked payloads, or null if not found.</returns>
    Task<LogActionDetailsDto?> GetDetailsAsync(
        Guid actionId,
        CancellationToken ct = default);
}
