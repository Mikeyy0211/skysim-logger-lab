using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Api.Base;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Api.Services.Query;
using FluentValidation;

namespace Skysim.Logger.Api.Controllers;

/// <summary>
/// API controller for querying log flows and their associated actions.
/// Provides endpoints for listing, filtering, and retrieving detailed flow information.
/// </summary>
[Route("api/log-flows")]
[Authorize]
[Produces("application/json")]
public class LogFlowsController : ApiControllerBase
{
    private readonly ILogFlowQueryService _flowQueryService;
    private readonly ILogActionQueryService _actionQueryService;
    private readonly IValidator<LogFlowListQuery> _listValidator;
    private readonly IValidator<LogActionListQuery> _actionListValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogFlowsController"/> class.
    /// </summary>
    /// <param name="flowQueryService">Service for querying log flow data.</param>
    /// <param name="actionQueryService">Service for querying log action data.</param>
    /// <param name="listValidator">Validator for log flow list query parameters.</param>
    /// <param name="actionListValidator">Validator for log action list query parameters.</param>
    public LogFlowsController(
        ILogFlowQueryService flowQueryService,
        ILogActionQueryService actionQueryService,
        IValidator<LogFlowListQuery> listValidator,
        IValidator<LogActionListQuery> actionListValidator)
    {
        _flowQueryService = flowQueryService;
        _actionQueryService = actionQueryService;
        _listValidator = listValidator;
        _actionListValidator = actionListValidator;
    }

    /// <summary>
    /// Retrieves a paginated list of log flows with optional filtering.
    /// </summary>
    /// <param name="query">Query parameters for filtering, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>Paginated list of log flow summaries.</returns>
    /// <response code="200">Returns the paginated list of log flows.</response>
    /// <response code="400">If the query parameters are invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<LogFlowSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<LogFlowSummaryDto>>> GetLogFlows(
        [FromQuery] LogFlowListQuery query,
        CancellationToken cancellationToken)
    {
        return await ValidateAndQueryAsync(
            _listValidator,
            query,
            ct => _flowQueryService.GetListAsync(query, ct),
            cancellationToken);
    }

    /// <summary>
    /// Retrieves detailed information for a specific log flow by its flow ID.
    /// </summary>
    /// <param name="flowId">The unique identifier of the flow.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>Detailed log flow information including associated actions.</returns>
    /// <response code="200">Returns the log flow details.</response>
    /// <response code="404">If the flow with the specified ID is not found.</response>
    [HttpGet("{flowId}")]
    [ProducesResponseType(typeof(LogFlowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LogFlowDetailDto>> GetLogFlowById(
        string flowId,
        CancellationToken cancellationToken)
    {
        var result = await _flowQueryService.GetByFlowIdAsync(flowId, cancellationToken);
        return NotFoundIfNull(result, "Flow", flowId);
    }

    /// <summary>
    /// Retrieves a paginated list of actions for a specific flow.
    /// </summary>
    /// <param name="flowId">The unique identifier of the flow.</param>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>Paginated list of log actions for the specified flow.</returns>
    /// <response code="200">Returns the paginated list of log actions.</response>
    /// <response code="400">If the query parameters are invalid.</response>
    /// <response code="404">If the flow with the specified ID is not found.</response>
    [HttpGet("{flowId}/actions")]
    [ProducesResponseType(typeof(PagedResponse<LogActionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<LogActionDto>>> GetActionsByFlowId(
        string flowId,
        [FromQuery] LogActionListQuery query,
        CancellationToken cancellationToken)
    {
        query.FlowId = flowId;

        var flowExists = await _flowQueryService.FlowExistsAsync(flowId, cancellationToken);
        if (!flowExists)
        {
            return NotFound(new ApiErrorResponse(
                new ApiErrorDetail("flow_not_found", $"Flow with id '{flowId}' was not found.", null)));
        }

        return await ValidateAndQueryAsync(
            _actionListValidator,
            query,
            ct => _actionQueryService.GetByFlowIdAsync(query, ct),
            cancellationToken);
    }
}
