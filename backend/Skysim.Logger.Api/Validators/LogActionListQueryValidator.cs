using FluentValidation;
using Skysim.Logger.Api.Contracts.DTOs.Queries;

namespace Skysim.Logger.Api.Validators;

public class LogActionListQueryValidator : AbstractValidator<LogActionListQuery>
{
    public LogActionListQueryValidator()
    {
        RuleFor(q => q.FlowId)
            .NotEmpty()
            .WithMessage("FlowId is required.");

        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(q => q.ServiceName)
            .MaximumLength(50)
            .When(q => !string.IsNullOrWhiteSpace(q.ServiceName));
    }
}
