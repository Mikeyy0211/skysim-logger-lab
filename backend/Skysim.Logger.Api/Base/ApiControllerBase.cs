using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Skysim.Logger.Contracts.DTOs;

namespace Skysim.Logger.Api.Base;

/// <summary>
/// Base class for all API controllers in the Logger service.
/// Provides common validation and response helper methods.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Validates a query using the specified validator and executes the query if valid.
    /// </summary>
    /// <typeparam name="T">The type of the response data.</typeparam>
    /// <typeparam name="TQuery">The type of the query to validate.</typeparam>
    /// <param name="validator">The validator to use for query validation.</param>
    /// <param name="query">The query to validate and execute.</param>
    /// <param name="execute">The function to execute if validation passes.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>An action result containing either the query results or a validation error.</returns>
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

    /// <summary>
    /// Returns a 404 Not Found response if the result is null, otherwise returns the result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="result">The result to check.</param>
    /// <param name="entityName">The name of the entity for the error message.</param>
    /// <param name="id">The ID of the entity for the error message.</param>
    /// <returns>Ok result if not null, otherwise NotFound result.</returns>
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
