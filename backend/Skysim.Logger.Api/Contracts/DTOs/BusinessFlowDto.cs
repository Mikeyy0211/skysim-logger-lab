namespace Skysim.Logger.Api.Contracts.DTOs;

/// <summary>
/// Represents a grouped business flow summary grouped by orderCode.
/// </summary>
public record BusinessFlowSummaryDto(
    string OrderCode,
    string RepresentativeFlowId,
    string? UserEmail,
    string? CustomerEmail,
    string? CustomerPhone,
    string? PartnerId,
    string? PaymentId,
    string? TransactionId,
    string OverallStatus,
    List<string> Services,
    int ActionCount,
    int FailedCount,
    int SuccessCount,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    string? LastMessage,
    string? LastServiceName,
    string? LastActionType);

/// <summary>
/// Represents the full detail of a business flow, including all grouped flows and actions.
/// </summary>
public record BusinessFlowDetailDto(
    BusinessFlowSummaryDto Summary,
    List<BusinessFlowActionDto> Timeline);

/// <summary>
/// Represents a single action within a business flow timeline.
/// Includes payload summaries loaded lazily from the detail endpoint.
/// </summary>
public record BusinessFlowActionDto(
    string FlowId,
    Guid EventId,
    string ServiceName,
    string ActionType,
    string Status,
    string? Message,
    string? ErrorCode,
    string? ErrorMessage,
    int? DurationMs,
    string? CorrelationId,
    DateTime CreatedAt,
    DateTime? RequestTime,
    DateTime? ResponseTime,
    string? RequestPayload,
    string? ResponsePayload,
    string? ErrorPayload,
    string? Metadata);
