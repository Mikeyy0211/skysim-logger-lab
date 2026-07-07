using System.Collections.Generic;

namespace Skysim.Logger.Api.Contracts.DTOs;

/// <summary>
/// Aggregate dashboard metrics for the log flows in the system.
/// Designed for business operators to get a quick health snapshot.
/// </summary>
/// <param name="TotalFlows">Total number of flows across all statuses.</param>
/// <param name="TotalActions">Total number of log actions (HTTP requests + business steps).</param>
/// <param name="LogsToday">Number of flows created since server midnight (UTC).</param>
/// <param name="LogsThisWeek">Number of flows created since Monday 00:00 of the current week (UTC).</param>
/// <param name="SuccessFlows">Number of flows with status SUCCESS.</param>
/// <param name="FailedFlows">Number of flows with status FAILED.</param>
/// <param name="RunningFlows">Number of flows with status RUNNING.</param>
/// <param name="PartialFailed">Number of flows with status PARTIAL_FAILED.</param>
/// <param name="SuccessRate">successFlows / totalFlows * 100. Returns 0 when totalFlows is 0.</param>
/// <param name="AverageDurationMs">Average duration in milliseconds for completed flows (startedAt -> completedAt); null if none.</param>
/// <param name="RecentFailedFlows">Up to 5 most recent FAILED flows, ordered by updatedAt desc.</param>
/// <param name="RecentSuccessFlows">Up to 5 most recent SUCCESS flows, ordered by updatedAt desc.</param>
public record DashboardMetricsDto(
    long TotalFlows,
    long TotalActions,
    long LogsToday,
    long LogsThisWeek,
    long SuccessFlows,
    long FailedFlows,
    long RunningFlows,
    long PartialFailed,
    double SuccessRate,
    double? AverageDurationMs,
    IReadOnlyList<RecentFlowDto> RecentFailedFlows,
    IReadOnlyList<RecentFlowDto> RecentSuccessFlows);