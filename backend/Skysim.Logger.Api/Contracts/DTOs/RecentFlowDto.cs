namespace Skysim.Logger.Api.Contracts.DTOs;

/// <summary>
/// Compact projection of a log flow used by dashboard recent lists.
/// </summary>
/// <param name="FlowId">Unique flow id.</param>
/// <param name="Status">Flow status (SUCCESS, FAILED, RUNNING, PARTIAL_FAILED, ...).</param>
/// <param name="UserId">Internal user id from JWT (may be null for guest checkout).</param>
/// <param name="UserEmail">Authenticated user email extracted from JWT (if any).</param>
/// <param name="Username">Authenticated username extracted from JWT (if any).</param>
/// <param name="CustomerEmail">Customer email captured in business payload.</param>
/// <param name="PartnerId">Partner id from JWT (if any).</param>
/// <param name="OrderCode">Business order code from payload.</param>
/// <param name="OrderId">Internal order id from payload.</param>
/// <param name="PaymentId">Payment id from payload.</param>
/// <param name="TransactionId">Transaction id from payload.</param>
/// <param name="LastServiceName">Service name of the latest meaningful action (null if no actions).</param>
/// <param name="LastActionType">Action type of the latest action.</param>
/// <param name="LastMessage">Last message recorded on the flow.</param>
/// <param name="LastDurationMs">Duration in ms of the latest action (null if not available).</param>
/// <param name="UpdatedAt">Last updated time (preferred for display).</param>
/// <param name="CreatedAt">Creation time (fallback display).</param>
public record RecentFlowDto(
    string FlowId,
    string Status,
    string? UserId,
    string? UserEmail,
    string? Username,
    string? CustomerEmail,
    string? PartnerId,
    string? OrderCode,
    string? OrderId,
    string? PaymentId,
    string? TransactionId,
    string? LastServiceName,
    string? LastActionType,
    string? LastMessage,
    int? LastDurationMs,
    DateTime UpdatedAt,
    DateTime CreatedAt);