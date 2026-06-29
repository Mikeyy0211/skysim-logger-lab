using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Api.Base;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Contracts.DTOs;
using Skysim.Logger.Api.Services.Query;

namespace Skysim.Logger.Api.Controllers;

/// <summary>
/// API controller for querying log action details.
/// Provides endpoints for retrieving detailed information about individual log actions.
/// </summary>
[Route("api/log-actions")]
[Authorize]
[Produces("application/json")]
public class LogActionsController : ApiControllerBase
{
    private readonly ILogActionQueryService _actionQueryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogActionsController"/> class.
    /// </summary>
    /// <param name="actionQueryService">Service for querying log action data.</param>
    public LogActionsController(ILogActionQueryService actionQueryService)
    {
        _actionQueryService = actionQueryService;
    }

    /// <summary>
    /// Retrieves detailed information for a specific log action by its action ID.
    /// Includes request/response payloads with sensitive data masked.
    /// </summary>
    /// <param name="actionId">The unique identifier of the action.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>Detailed log action information including masked payloads.</returns>
    /// <response code="200">Returns the log action details.</response>
    /// <response code="404">If the action with the specified ID is not found.</response>
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
