using FluentValidation;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using CheckoutTypes = Skysim.Logger.Contracts.Constants.CheckoutTypes;

namespace Skysim.Logger.Api.Validators;

public class LogEventMessageValidator : AbstractValidator<LogEventMessage>
{
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

        RuleFor(x => x.Status)
            .Must(status => status == StatusTypes.Success || status == StatusTypes.Failed || status == StatusTypes.InProgress)
            .WithMessage("status must be one of: SUCCESS, FAILED, IN_PROGRESS");

        RuleFor(x => x.CreatedAt)
            .NotEmpty()
            .WithMessage("createdAt is required")
            .Must(BeValidIso8601Utc)
            .WithMessage("createdAt must be a valid ISO-8601 UTC timestamp");

        RuleFor(x => x.FlowType)
            .Must(flowType => flowType == FlowTypes.CheckoutEsim || flowType == FlowTypes.HttpAction)
            .WithMessage("flowType must be a valid FlowType value");

        RuleFor(x => x.CheckoutType)
            .Must(checkoutType => checkoutType == CheckoutTypes.Guest || checkoutType == CheckoutTypes.Authenticated)
            .When(x => !string.IsNullOrEmpty(x.CheckoutType))
            .WithMessage("checkoutType must be one of: GUEST, AUTHENTICATED");

        RuleFor(x => x.RequestTime)
            .Must(dt => !dt.HasValue || BeValidIso8601Utc(dt.Value))
            .When(x => x.RequestTime.HasValue)
            .WithMessage("requestTime must be a valid ISO-8601 UTC timestamp");

        RuleFor(x => x.ResponseTime)
            .Must(dt => !dt.HasValue || BeValidIso8601Utc(dt.Value))
            .When(x => x.ResponseTime.HasValue)
            .WithMessage("responseTime must be a valid ISO-8601 UTC timestamp");
    }

    private static bool BeValidIso8601Utc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Utc || dt.Kind == DateTimeKind.Unspecified;
    }
}
