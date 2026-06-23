using FluentValidation;
using Skysim.Logger.Api.Domain.Enums;

namespace Skysim.Logger.Api.Contracts.DTOs;

public class LogEventMessageValidator : AbstractValidator<LogEventMessage>
{
    private static readonly ActionType[] CanonicalActionTypes =
    [
        ActionType.OrderCreated,
        ActionType.PaymentRequested,
        ActionType.PaymentSuccess,
        ActionType.ProviderRequested,
        ActionType.EsimActivated,
        ActionType.EmailSent,
        ActionType.OrderFailed,
        ActionType.PaymentFailed,
        ActionType.ProviderFailed,
        ActionType.EsimActivationFailed,
        ActionType.EmailFailed
    ];

    public LogEventMessageValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("eventId is required");

        RuleFor(x => x.FlowId)
            .NotEmpty()
            .WithMessage("flowId is required")
            .MaximumLength(100)
            .WithMessage("flowId must not exceed 100 characters");

        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("serviceName is required");

        RuleFor(x => x.ActionType)
            .Must(BeAValidActionType)
            .WithMessage(x => $"actionType '{x.ActionType}' is not in the canonical list");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("status must be one of: SUCCESS, FAILED, IN_PROGRESS");

        RuleFor(x => x.CreatedAt)
            .NotEmpty()
            .WithMessage("createdAt is required")
            .Must(BeValidIso8601Utc)
            .WithMessage("createdAt must be a valid ISO-8601 UTC timestamp");

        RuleFor(x => x.FlowType)
            .IsInEnum()
            .WithMessage("flowType must be a valid FlowType value");

        RuleFor(x => x.CheckoutType)
            .IsInEnum()
            .When(x => x.CheckoutType.HasValue)
            .WithMessage("checkoutType must be one of: GUEST, AUTHENTICATED");

        RuleFor(x => x.RequestTime)
            .Must(dt => dt.HasValue == false || BeValidIso8601Utc(dt.Value))
            .When(x => x.RequestTime.HasValue)
            .WithMessage("requestTime must be a valid ISO-8601 UTC timestamp");

        RuleFor(x => x.ResponseTime)
            .Must(dt => dt.HasValue == false || BeValidIso8601Utc(dt.Value))
            .When(x => x.ResponseTime.HasValue)
            .WithMessage("responseTime must be a valid ISO-8601 UTC timestamp");
    }

    private static bool BeAValidActionType(ActionType actionType)
    {
        return CanonicalActionTypes.Contains(actionType);
    }

    private static bool BeValidIso8601Utc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Utc || dt.Kind == DateTimeKind.Unspecified;
    }
}
