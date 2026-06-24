namespace Skysim.Logger.Api.Contracts.DTOs;

public record LogActionDto(
    Guid Id,
    Guid EventId,
    string FlowId,
    int StepOrder,
    string ServiceName,
    string ActionType,
    string Status,
    string? Message,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime? RequestTime,
    DateTime? ResponseTime,
    int? DurationMs,
    string? CorrelationId,
    DateTime CreatedAt);
