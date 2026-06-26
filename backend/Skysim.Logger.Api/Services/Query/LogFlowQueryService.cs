using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;
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

        q = ApplySorting(q, query.SortBy, query.SortDirection);

        var totalItems = await q.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new LogFlowSummaryDto(
                f.FlowId,
                f.FlowType,
                f.CheckoutType,
                f.Status,
                f.CustomerEmail,
                f.CustomerPhone,
                f.UserId,
                f.OrderId,
                f.PaymentId,
                f.TotalSteps,
                f.SuccessSteps,
                f.FailedSteps,
                f.LastActionType,
                f.LastMessage,
                f.StartedAt,
                f.CompletedAt,
                f.CreatedAt,
                f.UpdatedAt))
            .ToListAsync(ct);

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

        var summary = new LogFlowSummaryDto(
            flow.FlowId,
            flow.FlowType,
            flow.CheckoutType,
            flow.Status,
            flow.CustomerEmail,
            flow.CustomerPhone,
            flow.UserId,
            flow.OrderId,
            flow.PaymentId,
            flow.TotalSteps,
            flow.SuccessSteps,
            flow.FailedSteps,
            flow.LastActionType,
            flow.LastMessage,
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

        var effectiveSortBy = (sortBy ?? "createdAt").ToLowerInvariant();

        if (effectiveSortBy == "status")
        {
            return isDesc
                ? query.OrderByDescending(f => f.Status).ThenByDescending(f => f.CreatedAt)
                : query.OrderBy(f => f.Status).ThenByDescending(f => f.CreatedAt);
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
            _ => query.OrderByDescending(f => f.CreatedAt)
        };
    }
}
