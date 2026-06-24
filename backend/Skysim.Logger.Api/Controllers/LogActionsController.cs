using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Services.Query;

namespace Skysim.Logger.Api.Controllers;

/// <summary>
/// Read-only API for querying individual action details with masked payloads.
/// </summary>
[ApiController]
[Route("api/log-actions")]
[Produces("application/json")]
public class LogActionsController : ControllerBase
{
    private readonly ILogActionQueryService _actionQueryService;

    public LogActionsController(ILogActionQueryService actionQueryService)
    {
        _actionQueryService = actionQueryService;
    }

    /// <summary>
    /// Returns the full action details including masked payloads (request, response, error, metadata).
    /// </summary>
    [HttpGet("{actionId}/details")]
    [ProducesResponseType(typeof(LogActionDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LogActionDetailsDto>> GetActionDetails(
        Guid actionId,
        CancellationToken cancellationToken)
    {
        var result = await _actionQueryService.GetDetailsAsync(actionId, cancellationToken);

        if (result == null)
        {
            return NotFound(new ApiErrorResponse(
                new ApiErrorDetail("action_not_found",
                    $"Action with id '{actionId}' was not found.", null)));
        }

        return Ok(result);
    }
}
