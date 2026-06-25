using System.Text.Json;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Domain.Entities;
using ActionType = Skysim.Logger.Api.Domain.Enums.ActionType;
using Status = Skysim.Logger.Api.Domain.Enums.Status;

namespace Skysim.Logger.Api.Domain.Factories;

public static class LogActionFactory
{
    private static readonly JsonSerializerOptions JsonOptions = LogEventMessage.JsonOptions;

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
            ActionType = SerializeEnum(message.ActionType),
            Status = SerializeEnum(message.Status),
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

    private static string SerializeEnum<T>(T enumValue) where T : struct, Enum
    {
        return JsonSerializer.Serialize(enumValue, JsonOptions).Trim('"');
    }
}
