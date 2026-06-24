using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;
using Skysim.Logger.Api.Services.Query;

namespace Skysim.Logger.Api.Controllers;

/// <summary>
/// Read-only API for querying log flows and their action timelines.
/// </summary>
[ApiController]
[Route("api/log-flows")]
[Produces("application/json")]
public class LogFlowsController : ControllerBase
{
    private readonly ILogFlowQueryService _flowQueryService;
    private readonly ILogActionQueryService _actionQueryService;
    private readonly IValidator<LogFlowListQuery> _listValidator;
    private readonly IValidator<LogActionListQuery> _actionListValidator;

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
    /// Returns a paginated, filterable list of log flow summaries.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<LogFlowSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<LogFlowSummaryDto>>> GetLogFlows(
        [FromQuery] LogFlowListQuery query,
        CancellationToken cancellationToken)
    {
        var validationResult = await _listValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(e => new ApiFieldError(e.PropertyName, e.ErrorMessage))
                .ToList();
            return BadRequest(new ApiErrorResponse(
                new ApiErrorDetail("validation_error", "Invalid query parameters.", errors)));
        }

        var result = await _flowQueryService.GetListAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single log flow with its ordered action timeline.
    /// </summary>
    [HttpGet("{flowId}")]
    [ProducesResponseType(typeof(LogFlowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LogFlowDetailDto>> GetLogFlowById(
        string flowId,
        CancellationToken cancellationToken)
    {
        var result = await _flowQueryService.GetByFlowIdAsync(flowId, cancellationToken);

        if (result == null)
        {
            return NotFound(new ApiErrorResponse(
                new ApiErrorDetail("flow_not_found", $"Flow with id '{flowId}' was not found.", null)));
        }

        return Ok(result);
    }

    /// <summary>
    /// Returns a paginated list of actions for a specific log flow.
    /// </summary>
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

        var validationResult = await _actionListValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(e => new ApiFieldError(e.PropertyName, e.ErrorMessage))
                .ToList();
            return BadRequest(new ApiErrorResponse(
                new ApiErrorDetail("validation_error", "Invalid query parameters.", errors)));
        }

        var flowExists = await _flowQueryService.GetByFlowIdAsync(flowId, cancellationToken);
        if (flowExists == null)
        {
            return NotFound(new ApiErrorResponse(
                new ApiErrorDetail("flow_not_found", $"Flow with id '{flowId}' was not found.", null)));
        }

        var result = await _actionQueryService.GetByFlowIdAsync(query, cancellationToken);
        return Ok(result);
    }
}
