using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Contracts.DTOs;
using Skysim.Logger.Contracts.Constants;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Api.Services.Query;

/// <summary>
/// Implementation of <see cref="ILogFlowQueryService"/> for querying log flow data.
/// </summary>
public class LogFlowQueryService : ILogFlowQueryService
{
    private readonly IDbContextFactory<LoggerDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogFlowQueryService"/> class.
    /// </summary>
    /// <param name="dbContextFactory">Factory for creating database contexts.</param>
    public LogFlowQueryService(IDbContextFactory<LoggerDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<PagedResponse<LogFlowSummaryDto>> GetListAsync(
        LogFlowListQuery query,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.LogFlows.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.CustomerEmail))
        {
            q = q.Where(f => f.CustomerEmail != null &&
                             f.CustomerEmail.ToLower() == query.CustomerEmail.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerPhone))
        {
            q = q.Where(f => f.CustomerPhone == query.CustomerPhone);
        }

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            q = q.Where(f => f.UserId == query.UserId);
        }

        if (!string.IsNullOrWhiteSpace(query.OrderId))
        {
            q = q.Where(f => f.OrderId == query.OrderId);
        }

        if (!string.IsNullOrWhiteSpace(query.PaymentId))
        {
            q = q.Where(f => f.PaymentId == query.PaymentId);
        }

        if (!string.IsNullOrWhiteSpace(query.FlowType))
        {
            q = q.Where(f => f.FlowType == query.FlowType);
        }

        if (!string.IsNullOrWhiteSpace(query.CheckoutType))
        {
            q = q.Where(f => f.CheckoutType == query.CheckoutType);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            q = q.Where(f => f.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.ServiceName))
        {
            var matchingFlowIds = db.LogActions
                .AsNoTracking()
                .Where(a => a.ServiceName == query.ServiceName)
                .Select(a => a.FlowId)
                .Distinct();
            q = q.Where(f => matchingFlowIds.Contains(f.FlowId));
        }

        if (!string.IsNullOrWhiteSpace(query.FromDate) &&
            DateTime.TryParse(query.FromDate, out var fromDate))
        {
            var utcFrom = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
            q = q.Where(f => f.CreatedAt >= utcFrom);
        }

        if (!string.IsNullOrWhiteSpace(query.ToDate) &&
            DateTime.TryParse(query.ToDate, out var toDate))
        {
            var utcTo = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            q = q.Where(f => f.CreatedAt <= utcTo);
        }

        q = ApplySearchPredicate(q, db, query.Search);
        q = ApplySorting(q, query.SortBy, query.SortDirection);

        var totalItems = await q.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var flows = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (flows.Count == 0)
        {
            return new PagedResponse<LogFlowSummaryDto>(
                new List<LogFlowSummaryDto>(), page, pageSize, totalItems, totalPages);
        }

        var flowIds = flows.Select(f => f.FlowId).ToList();
        var allActions = await db.LogActions
            .AsNoTracking()
            .Where(a => flowIds.Contains(a.FlowId))
            .ToListAsync(ct);

        var lastServiceNameMap = flows.ToDictionary(
            f => f.FlowId,
            f => ComputeLastServiceName(f.FlowType, allActions.Where(a => a.FlowId == f.FlowId).ToList()));

        var items = flows.Select(f => new LogFlowSummaryDto(
            f.FlowId,
            f.FlowType,
            f.CheckoutType,
            f.Status,
            f.UserId,
            f.UserEmail,
            f.Username,
            f.PartnerId,
            f.CustomerEmail,
            f.CustomerPhone,
            f.OrderId,
            f.OrderCode,
            f.PaymentId,
            f.TransactionId,
            f.TotalSteps,
            f.SuccessSteps,
            f.FailedSteps,
            f.LastActionType,
            f.LastMessage,
            lastServiceNameMap[f.FlowId],
            ComputeDurationMs(f.StartedAt, f.CompletedAt),
            f.StartedAt,
            f.CompletedAt,
            f.CreatedAt,
            f.UpdatedAt))
            .ToList();

        return new PagedResponse<LogFlowSummaryDto>(items, page, pageSize, totalItems, totalPages);
    }

    /// <inheritdoc />
    public async Task<LogFlowDetailDto?> GetByFlowIdAsync(
        string flowId,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var flow = await db.LogFlows
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FlowId == flowId, ct);

        if (flow == null)
        {
            return null;
        }

        var actions = await db.LogActions
            .AsNoTracking()
            .Where(a => a.FlowId == flowId)
            .OrderBy(a => a.StepOrder)
            .Select(a => new LogActionDto(
                a.Id,
                a.EventId,
                a.FlowId,
                a.StepOrder,
                a.ServiceName,
                a.ActionType,
                a.Status,
                a.Message,
                a.ErrorCode,
                a.ErrorMessage,
                a.RequestTime,
                a.ResponseTime,
                a.DurationMs,
                a.CorrelationId,
                a.CreatedAt))
            .ToListAsync(ct);

        var lastServiceName = actions
            .OrderByDescending(a => a.StepOrder)
            .FirstOrDefault(a => flow.FlowType != FlowTypes.CheckoutEsim || a.ActionType != ActionTypes.HttpRequest)
            ?.ServiceName;

        var summary = new LogFlowSummaryDto(
            flow.FlowId,
            flow.FlowType,
            flow.CheckoutType,
            flow.Status,
            flow.UserId,
            flow.UserEmail,
            flow.Username,
            flow.PartnerId,
            flow.CustomerEmail,
            flow.CustomerPhone,
            flow.OrderId,
            flow.OrderCode,
            flow.PaymentId,
            flow.TransactionId,
            flow.TotalSteps,
            flow.SuccessSteps,
            flow.FailedSteps,
            flow.LastActionType,
            flow.LastMessage,
            lastServiceName,
            ComputeDurationMs(flow.StartedAt, flow.CompletedAt),
            flow.StartedAt,
            flow.CompletedAt,
            flow.CreatedAt,
            flow.UpdatedAt);

        return new LogFlowDetailDto(summary, actions);
    }

    /// <inheritdoc />
    public async Task<bool> FlowExistsAsync(
        string flowId,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        return await db.LogFlows
            .AsNoTracking()
            .AnyAsync(f => f.FlowId == flowId, ct);
    }

    /// <inheritdoc />
    public async Task<DashboardMetricsDto> GetDashboardMetricsAsync(
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var statusCounts = await db.LogFlows
            .AsNoTracking()
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        long success = 0;
        long failed = 0;
        long running = 0;
        long partial = 0;

        foreach (var row in statusCounts)
        {
            if (row.Status == StatusTypes.Success) success = row.Count;
            else if (row.Status == StatusTypes.Failed) failed = row.Count;
            else if (row.Status == StatusTypes.Running) running = row.Count;
            else if (row.Status == StatusTypes.PartialFailed) partial = row.Count;
        }

        long total = success + failed + running + partial;

        var nowUtc = DateTime.UtcNow;
        var todayUtc = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc);
        var weekStartUtc = StartOfWeekUtc(nowUtc);

        long totalActions = await db.LogActions.AsNoTracking().LongCountAsync(ct);

        long logsToday = await db.LogFlows
            .AsNoTracking()
            .Where(f => f.CreatedAt >= todayUtc)
            .LongCountAsync(ct);

        long logsThisWeek = await db.LogFlows
            .AsNoTracking()
            .Where(f => f.CreatedAt >= weekStartUtc)
            .LongCountAsync(ct);

        double successRate = total > 0
            ? Math.Round((double)success / total * 100, 2)
            : 0d;

        double? averageDurationMs = await db.LogFlows
            .AsNoTracking()
            .Where(f => f.CompletedAt != null)
            .Select(f => (double?)(f.CompletedAt!.Value - f.StartedAt).TotalMilliseconds)
            .AverageAsync(ct);

        var recentFailed = await LoadRecentFlowsAsync(db, StatusTypes.Failed, RecentLimit, ct);
        var recentSuccess = await LoadRecentFlowsAsync(db, StatusTypes.Success, RecentLimit, ct);

        return new DashboardMetricsDto(
            total,
            totalActions,
            logsToday,
            logsThisWeek,
            success,
            failed,
            running,
            partial,
            successRate,
            averageDurationMs,
            recentFailed,
            recentSuccess);
    }

    /// <summary>
    /// Loads up to <paramref name="limit"/> most recent flows for a given status, including the
    /// service name, action type, message, and duration of the latest meaningful action.
    /// </summary>
    private static async Task<IReadOnlyList<RecentFlowDto>> LoadRecentFlowsAsync(
        LoggerDbContext db,
        string status,
        int limit,
        CancellationToken ct)
    {
        var flows = await db.LogFlows
            .AsNoTracking()
            .Where(f => f.Status == status)
            .OrderByDescending(f => f.UpdatedAt)
            .ThenByDescending(f => f.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        if (flows.Count == 0)
        {
            return Array.Empty<RecentFlowDto>();
        }

        var flowIds = flows.Select(f => f.FlowId).ToList();
        var latestActionsByFlow = await db.LogActions
            .AsNoTracking()
            .Where(a => flowIds.Contains(a.FlowId))
            .GroupBy(a => a.FlowId)
            .Select(g => g.OrderByDescending(a => a.StepOrder)
                          .ThenByDescending(a => a.CreatedAt)
                          .FirstOrDefault())
            .ToListAsync(ct);

        var actionLookup = latestActionsByFlow
            .Where(a => a != null)
            .ToDictionary(a => a!.FlowId, a => a!);

        return flows.Select(f =>
        {
            actionLookup.TryGetValue(f.FlowId, out var lastAction);
            return new RecentFlowDto(
                f.FlowId,
                f.Status,
                f.UserId,
                f.UserEmail,
                f.Username,
                f.CustomerEmail,
                f.PartnerId,
                f.OrderCode,
                f.OrderId,
                f.PaymentId,
                f.TransactionId,
                lastAction?.ServiceName,
                lastAction?.ActionType,
                f.LastMessage,
                lastAction?.DurationMs,
                f.UpdatedAt,
                f.CreatedAt);
        }).ToList();
    }

    private const int RecentLimit = 5;

    /// <summary>
    /// Returns Monday 00:00 (UTC) of the week containing <paramref name="nowUtc"/>.
    /// Uses ISO 8601 week rule (Monday is the first day of the week).
    /// </summary>
    private static DateTime StartOfWeekUtc(DateTime nowUtc)
    {
        var date = nowUtc.Date;
        var dayOfWeek = (int)date.DayOfWeek;
        // DayOfWeek.Sunday == 0 -> treat Sunday as last day => offset 6; Monday == 1 -> offset 0.
        var offsetToMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return DateTime.SpecifyKind(date.AddDays(-offsetToMonday), DateTimeKind.Utc);
    }

    /// <summary>
    /// Applies the unified search predicate across multiple fields using case-insensitive matching.
    /// </summary>
    private static IQueryable<LogFlow> ApplySearchPredicate(
        IQueryable<LogFlow> query,
        LoggerDbContext db,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var searchPattern = $"%{search}%";
        var isNpgsql = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        if (isNpgsql)
        {
            return query.Where(f =>
                (f.FlowId != null && EF.Functions.ILike(f.FlowId, searchPattern)) ||
                (f.UserEmail != null && EF.Functions.ILike(f.UserEmail, searchPattern)) ||
                (f.Username != null && EF.Functions.ILike(f.Username, searchPattern)) ||
                (f.PartnerId != null && EF.Functions.ILike(f.PartnerId, searchPattern)) ||
                (f.CustomerEmail != null && EF.Functions.ILike(f.CustomerEmail, searchPattern)) ||
                (f.CustomerPhone != null && EF.Functions.ILike(f.CustomerPhone, searchPattern)) ||
                (f.OrderCode != null && EF.Functions.ILike(f.OrderCode, searchPattern)) ||
                (f.OrderId != null && EF.Functions.ILike(f.OrderId, searchPattern)) ||
                (f.PaymentId != null && EF.Functions.ILike(f.PaymentId, searchPattern)) ||
                (f.TransactionId != null && EF.Functions.ILike(f.TransactionId, searchPattern)) ||
                (f.UserId != null && EF.Functions.ILike(f.UserId, searchPattern)) ||
                (f.LastMessage != null && EF.Functions.ILike(f.LastMessage, searchPattern)));
        }

        var lowerSearch = search.ToLowerInvariant();
        return query.Where(f =>
            (f.FlowId != null && f.FlowId.ToLower().Contains(lowerSearch)) ||
            (f.UserEmail != null && f.UserEmail.ToLower().Contains(lowerSearch)) ||
            (f.Username != null && f.Username.ToLower().Contains(lowerSearch)) ||
            (f.PartnerId != null && f.PartnerId.ToLower().Contains(lowerSearch)) ||
            (f.CustomerEmail != null && f.CustomerEmail.ToLower().Contains(lowerSearch)) ||
            (f.CustomerPhone != null && f.CustomerPhone.ToLower().Contains(lowerSearch)) ||
            (f.OrderCode != null && f.OrderCode.ToLower().Contains(lowerSearch)) ||
            (f.OrderId != null && f.OrderId.ToLower().Contains(lowerSearch)) ||
            (f.PaymentId != null && f.PaymentId.ToLower().Contains(lowerSearch)) ||
            (f.TransactionId != null && f.TransactionId.ToLower().Contains(lowerSearch)) ||
            (f.UserId != null && f.UserId.ToLower().Contains(lowerSearch)) ||
            (f.LastMessage != null && f.LastMessage.ToLower().Contains(lowerSearch)));
    }

    /// <summary>
    /// Applies sorting to the log flows query based on the specified sort field and direction.
    /// </summary>
    /// <param name="query">The query to apply sorting to.</param>
    /// <param name="sortBy">The field to sort by (createdAt, updatedAt, completedAt, status).</param>
    /// <param name="sortDirection">The sort direction (asc or desc).</param>
    /// <returns>The sorted query.</returns>
    private static IQueryable<LogFlow> ApplySorting(
        IQueryable<LogFlow> query,
        string? sortBy,
        string? sortDirection)
    {
        var isDesc = sortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) ?? true;

        var effectiveSortBy = (sortBy ?? "updatedAt").ToLowerInvariant();

        if (effectiveSortBy == "status")
        {
            return isDesc
                ? query.OrderByDescending(f => f.Status).ThenByDescending(f => f.UpdatedAt)
                : query.OrderBy(f => f.Status).ThenByDescending(f => f.UpdatedAt);
        }

        return effectiveSortBy switch
        {
            "createdat" => isDesc
                ? query.OrderByDescending(f => f.CreatedAt)
                : query.OrderBy(f => f.CreatedAt),
            "updatedat" => isDesc
                ? query.OrderByDescending(f => f.UpdatedAt)
                : query.OrderBy(f => f.UpdatedAt),
            "completedat" => isDesc
                ? query.OrderByDescending(f => f.CompletedAt)
                : query.OrderBy(f => f.CompletedAt),
            _ => query.OrderByDescending(f => f.UpdatedAt)
        };
    }

    /// <summary>
    /// Computes the lastServiceName for a flow based on its type and actions.
    /// For CHECKOUT_ESIM flows: ignores HTTP_REQUEST actions, returns service name of the latest business action.
    /// For HTTP_ACTION flows: returns service name of the latest action by stepOrder.
    /// </summary>
    private static string? ComputeLastServiceName(string flowType, List<LogAction> actions)
    {
        if (actions.Count == 0)
        {
            return null;
        }

        if (flowType == FlowTypes.CheckoutEsim)
        {
            var businessActions = actions
                .Where(a => a.ActionType != ActionTypes.HttpRequest)
                .OrderByDescending(a => a.StepOrder)
                .FirstOrDefault();
            return businessActions?.ServiceName;
        }

        return actions
            .OrderByDescending(a => a.StepOrder)
            .Select(a => a.ServiceName)
            .FirstOrDefault();
    }

    /// <summary>
    /// Computes total flow duration in milliseconds between startedAt and completedAt.
    /// Returns null if completedAt is not set.
    /// </summary>
    private static int? ComputeDurationMs(DateTime startedAt, DateTime? completedAt)
    {
        if (!completedAt.HasValue)
        {
            return null;
        }

        var diff = completedAt.Value - startedAt;
        return diff.TotalMilliseconds >= int.MaxValue ? null : (int?)diff.TotalMilliseconds;
    }
}
