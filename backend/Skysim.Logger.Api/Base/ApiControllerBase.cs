using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Api.Contracts.DTOs;

namespace Skysim.Logger.Api.Base;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected async Task<ActionResult<T>> ValidateAndQueryAsync<T, TQuery>(
        IValidator<TQuery> validator,
        TQuery query,
        Func<CancellationToken, Task<T>> execute,
        CancellationToken cancellationToken)
        where TQuery : class
    {
        var validationResult = await validator.ValidateAsync(query, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(e => new ApiFieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiErrorResponse(
                new ApiErrorDetail("validation_error", "Invalid query parameters.", errors)));
        }

        var result = await execute(cancellationToken);
        return Ok(result);
    }

    protected ActionResult<T> NotFoundIfNull<T>(T? result, string entityName, string id)
    {
        if (result == null)
        {
            return NotFound(new ApiErrorResponse(
                new ApiErrorDetail($"{entityName.ToLower()}_not_found",
                    $"{entityName} with id '{id}' was not found.", null)));
        }

        return Ok(result);
    }
}
