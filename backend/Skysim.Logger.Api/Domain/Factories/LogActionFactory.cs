using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Domain.Entities;

namespace Skysim.Logger.Api.Domain.Factories;

public static class LogActionFactory
{
    public static LogAction CreateFromMessage(LogEventMessage message, Guid flowId)
    {
        var durationMs = message.Duration;
        if (!durationMs.HasValue && message.RequestTime.HasValue && message.ResponseTime.HasValue)
        {
            durationMs = (int)(message.ResponseTime.Value - message.RequestTime.Value).TotalMilliseconds;
        }

        return new LogAction
        {
            Id = Guid.NewGuid(),
            EventId = message.EventId,
            FlowId = message.FlowId,
            StepOrder = 0,
            ServiceName = message.ServiceName,
            ActionType = message.ActionType.ToString(),
            Status = message.Status.ToString(),
            Message = message.Message,
            ErrorCode = message.ErrorCode,
            ErrorMessage = message.ErrorMessage,
            RequestTime = message.RequestTime,
            ResponseTime = message.ResponseTime,
            DurationMs = durationMs,
            CorrelationId = message.CorrelationId,
            CreatedAt = message.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
