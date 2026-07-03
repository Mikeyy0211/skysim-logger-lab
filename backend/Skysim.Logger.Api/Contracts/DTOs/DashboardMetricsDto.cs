namespace Skysim.Logger.Api.Contracts.DTOs;

/// <summary>
/// Aggregate dashboard metrics for the log flows in the system.
/// </summary>
/// <param name="TotalFlows">Total number of flows across all statuses.</param>
/// <param name="SuccessFlows">Number of flows with status SUCCESS.</param>
/// <param name="FailedFlows">Number of flows with status FAILED.</param>
/// <param name="RunningFlows">Number of flows with status RUNNING.</param>
/// <param name="PartialFailed">Number of flows with status PARTIAL_FAILED.</param>
/// <param name="AverageDurationMs">Average duration in milliseconds for completed flows (startedAt -> completedAt); null if none.</param>
public record DashboardMetricsDto(
    long TotalFlows,
    long SuccessFlows,
    long FailedFlows,
    long RunningFlows,
    long PartialFailed,
    double? AverageDurationMs);