using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Api.Base;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Services.Query;

namespace Skysim.Logger.Api.Controllers;

[Route("api/log-actions")]
[Produces("application/json")]
public class LogActionsController : ApiControllerBase
{
    private readonly ILogActionQueryService _actionQueryService;

    public LogActionsController(ILogActionQueryService actionQueryService)
    {
        _actionQueryService = actionQueryService;
    }

    [HttpGet("{actionId}/details")]
    [ProducesResponseType(typeof(LogActionDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LogActionDetailsDto>> GetActionDetails(
        Guid actionId,
        CancellationToken cancellationToken)
    {
        var result = await _actionQueryService.GetDetailsAsync(actionId, cancellationToken);
        return NotFoundIfNull(result, "Action", actionId.ToString());
    }
}
