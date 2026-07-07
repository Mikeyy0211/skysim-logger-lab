using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Api.Base;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Api.Services.Query;
using Skysim.Logger.Contracts.DTOs;

namespace Skysim.Logger.Api.Controllers;

/// <summary>
/// API controller for querying business flows grouped by order code.
/// Provides endpoints for listing and retrieving detailed business flow information
/// across multiple service requests within the same order.
/// </summary>
[Route("api/business-flows")]
[Authorize]
[Produces("application/json")]
public class BusinessFlowsController : ApiControllerBase
{
    private readonly IBusinessFlowQueryService _businessFlowQueryService;

    public BusinessFlowsController(IBusinessFlowQueryService businessFlowQueryService)
    {
        _businessFlowQueryService = businessFlowQueryService;
    }

    /// <summary>
    /// Retrieves a paginated list of business flows grouped by order code.
    /// Only flows with a non-null, non-empty orderCode are included.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>Paginated list of business flow summaries.</returns>
    /// <response code="200">Returns the paginated list of business flows.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<BusinessFlowSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<BusinessFlowSummaryDto>>> GetBusinessFlows(
        [FromQuery] BusinessFlowListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _businessFlowQueryService.GetListAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves detailed information for a specific business flow by its order code.
    /// Returns all flows and actions associated with the order code.
    /// </summary>
    /// <param name="orderCode">The order code identifying the business flow.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>Detailed business flow information including all associated actions.</returns>
    /// <response code="200">Returns the business flow details.</response>
    /// <response code="404">If no business flow with the specified order code is found.</response>
    [HttpGet("{orderCode}")]
    [ProducesResponseType(typeof(BusinessFlowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessFlowDetailDto>> GetBusinessFlowByOrderCode(
        string orderCode,
        CancellationToken cancellationToken)
    {
        var result = await _businessFlowQueryService.GetByOrderCodeAsync(orderCode, cancellationToken);
        return NotFoundIfNull(result, "BusinessFlow", orderCode);
    }
}
