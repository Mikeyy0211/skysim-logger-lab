using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Api.Base;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;
using Skysim.Logger.Api.Services.Query;
using FluentValidation;

namespace Skysim.Logger.Api.Controllers;

[Route("api/log-flows")]
[Produces("application/json")]
public class LogFlowsController : ApiControllerBase
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
