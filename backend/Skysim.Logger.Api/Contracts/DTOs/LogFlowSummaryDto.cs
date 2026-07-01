namespace Skysim.Logger.Api.Contracts.DTOs;

public record LogFlowSummaryDto(
    string FlowId,
    string FlowType,
    string? CheckoutType,
    string Status,
    string? CustomerEmail,
    string? CustomerPhone,
    string? UserId,
    string? OrderId,
    string? PaymentId,
    int TotalSteps,
    int SuccessSteps,
    int FailedSteps,
    string? LastActionType,
    string? LastMessage,
    string? LastServiceName,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
