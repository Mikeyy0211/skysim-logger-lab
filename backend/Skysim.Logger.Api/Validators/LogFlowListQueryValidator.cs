using FluentValidation;
using Skysim.Logger.Api.Contracts.Queries;

namespace Skysim.Logger.Api.Validators;

public class LogFlowListQueryValidator : AbstractValidator<LogFlowListQuery>
{
    private static readonly HashSet<string> ValidSortBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt",
        "updatedAt",
        "completedAt",
        "status"
    };

    public LogFlowListQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        RuleFor(q => q.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) || ValidSortBy.Contains(sortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", ValidSortBy)}.");

        RuleFor(q => q.SortDirection)
            .Must(dir => string.IsNullOrWhiteSpace(dir) ||
                         dir.Equals("asc", StringComparison.OrdinalIgnoreCase) ||
                         dir.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");

        RuleFor(q => q.FromDate)
            .Must(BeValidIso8601Date)
            .When(q => !string.IsNullOrWhiteSpace(q.FromDate))
            .WithMessage("FromDate must be a valid ISO-8601 date (e.g., 2026-06-01).");

        RuleFor(q => q.ToDate)
            .Must(BeValidIso8601Date)
            .When(q => !string.IsNullOrWhiteSpace(q.ToDate))
            .WithMessage("ToDate must be a valid ISO-8601 date (e.g., 2026-06-22).");

        RuleFor(q => q.CustomerEmail)
            .MaximumLength(255)
            .When(q => !string.IsNullOrWhiteSpace(q.CustomerEmail));

        RuleFor(q => q.CustomerPhone)
            .MaximumLength(30)
            .When(q => !string.IsNullOrWhiteSpace(q.CustomerPhone));

        RuleFor(q => q.UserId)
            .MaximumLength(100)
            .When(q => !string.IsNullOrWhiteSpace(q.UserId));

        RuleFor(q => q.OrderId)
            .MaximumLength(100)
            .When(q => !string.IsNullOrWhiteSpace(q.OrderId));

        RuleFor(q => q.PaymentId)
            .MaximumLength(100)
            .When(q => !string.IsNullOrWhiteSpace(q.PaymentId));

        RuleFor(q => q.FlowType)
            .MaximumLength(50)
            .When(q => !string.IsNullOrWhiteSpace(q.FlowType));

        RuleFor(q => q.CheckoutType)
            .MaximumLength(20)
            .When(q => !string.IsNullOrWhiteSpace(q.CheckoutType));

        RuleFor(q => q.Status)
            .MaximumLength(20)
            .When(q => !string.IsNullOrWhiteSpace(q.Status));

        RuleFor(q => q.ServiceName)
            .MaximumLength(50)
            .When(q => !string.IsNullOrWhiteSpace(q.ServiceName));
    }

    private static bool BeValidIso8601Date(string? dateString)
    {
        return !string.IsNullOrWhiteSpace(dateString) &&
               DateTime.TryParse(dateString, out _);
    }
}
