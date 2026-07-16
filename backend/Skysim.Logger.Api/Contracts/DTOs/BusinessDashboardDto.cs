namespace Skysim.Logger.Api.Contracts.DTOs;

/// <summary>
/// Business-first dashboard read model. Technical log rows remain unchanged in the database;
/// these values are grouped by a valid order code for operator-facing views.
/// </summary>
public record BusinessDashboardDto(
    long TotalOrders,
    long OrdersToday,
    long RunningOrders,
    long RequiresAttentionOrders,
    long CompletedOrders,
    double CompletionRate,
    IReadOnlyList<BusinessDashboardOrderDto> RecentRequiresAttention,
    IReadOnlyList<BusinessDashboardOrderDto> RecentCompleted,
    TechnicalDashboardSummaryDto TechnicalSummary);

public record BusinessDashboardOrderDto(
    string OrderCode,
    string? UserEmail,
    string? CustomerEmail,
    string? CustomerPhone,
    string? PaymentId,
    string? TransactionId,
    string Status,
    string? LastActionType,
    string? LastMessage,
    string? AttentionActionType,
    string? AttentionMessage,
    string? IssueSummary,
    DateTime FirstSeen,
    DateTime LastSeen,
    int TotalActions,
    int FailedActions,
    int TechnicalFlowCount);

/// <summary>
/// Compact technical health values shown below the business dashboard.
/// </summary>
public record TechnicalDashboardSummaryDto(
    long TotalFlows,
    long TotalActions,
    long LogsToday,
    long FailedFlows,
    double SuccessRate);
