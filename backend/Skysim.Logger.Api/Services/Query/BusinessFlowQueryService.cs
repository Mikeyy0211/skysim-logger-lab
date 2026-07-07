using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Contracts.DTOs;
using Skysim.Logger.Contracts.Constants;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Api.Services.Query;

/// <summary>
/// Implementation of <see cref="IBusinessFlowQueryService"/> for querying business flows
/// grouped by order code.
/// </summary>
public class BusinessFlowQueryService : IBusinessFlowQueryService
{
    private readonly IDbContextFactory<LoggerDbContext> _dbContextFactory;

    public BusinessFlowQueryService(IDbContextFactory<LoggerDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<PagedResponse<BusinessFlowSummaryDto>> GetListAsync(
        BusinessFlowListQuery query,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Only flows with a real orderCode
        var baseQuery = db.LogFlows
            .AsNoTracking()
            .Where(f => f.OrderCode != null && f.OrderCode.Length > 0);

        // Apply keyword search
        baseQuery = ApplyKeywordSearch(baseQuery, db, query.Keyword);

        // Get distinct orderCodes
        var orderCodeGroups = baseQuery
            .GroupBy(f => f.OrderCode)
            .Select(g => new
            {
                OrderCode = g.Key!,
                RepresentativeFlowId = g.OrderByDescending(f => f.UpdatedAt)
                                        .ThenByDescending(f => f.CreatedAt)
                                        .Select(f => f.FlowId)
                                        .First(),
                UserEmail = g.Where(f => f.UserEmail != null)
                             .OrderByDescending(f => f.UpdatedAt)
                             .ThenByDescending(f => f.CreatedAt)
                             .Select(f => f.UserEmail)
                             .FirstOrDefault(),
                CustomerEmail = g.Where(f => f.CustomerEmail != null)
                                 .OrderByDescending(f => f.UpdatedAt)
                                 .ThenByDescending(f => f.CreatedAt)
                                 .Select(f => f.CustomerEmail)
                                 .FirstOrDefault(),
                CustomerPhone = g.Where(f => f.CustomerPhone != null)
                                 .OrderByDescending(f => f.UpdatedAt)
                                 .ThenByDescending(f => f.CreatedAt)
                                 .Select(f => f.CustomerPhone)
                                 .FirstOrDefault(),
                PartnerId = g.Where(f => f.PartnerId != null)
                             .OrderByDescending(f => f.UpdatedAt)
                             .ThenByDescending(f => f.CreatedAt)
                             .Select(f => f.PartnerId)
                             .FirstOrDefault(),
                PaymentId = g.Where(f => f.PaymentId != null)
                             .OrderByDescending(f => f.UpdatedAt)
                             .ThenByDescending(f => f.CreatedAt)
                             .Select(f => f.PaymentId)
                             .FirstOrDefault(),
                TransactionId = g.Where(f => f.TransactionId != null)
                                  .OrderByDescending(f => f.UpdatedAt)
                                  .ThenByDescending(f => f.CreatedAt)
                                  .Select(f => f.TransactionId)
                                  .FirstOrDefault(),
                AllStatuses = g.Select(f => f.Status).ToList(),
                ActionCount = g.Sum(f => f.TotalSteps),
                FailedCount = g.Sum(f => f.FailedSteps),
                SuccessCount = g.Sum(f => f.SuccessSteps),
                FirstSeenAt = g.Min(f => f.CreatedAt),
                LastSeenAt = g.Max(f => f.UpdatedAt),
                LastMessage = g.Where(f => f.LastMessage != null)
                               .OrderByDescending(f => f.UpdatedAt)
                               .ThenByDescending(f => f.CreatedAt)
                               .Select(f => f.LastMessage)
                               .FirstOrDefault(),
                LastActionType = g.Where(f => f.LastActionType != null)
                                   .OrderByDescending(f => f.UpdatedAt)
                                   .ThenByDescending(f => f.CreatedAt)
                                   .Select(f => f.LastActionType)
                                   .FirstOrDefault(),
            })
            .OrderByDescending(x => x.LastSeenAt);

        var totalItems = await baseQuery.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var groups = await orderCodeGroups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (groups.Count == 0)
        {
            return new PagedResponse<BusinessFlowSummaryDto>(
                new List<BusinessFlowSummaryDto>(), page, pageSize, totalItems, totalPages);
        }

        // Load services and last service name for each representative flow
        var representativeFlowIds = groups.Select(g => g.RepresentativeFlowId).ToList();
        var latestActions = await db.LogActions
            .AsNoTracking()
            .Where(a => representativeFlowIds.Contains(a.FlowId))
            .GroupBy(a => a.FlowId)
            .Select(g => g.OrderByDescending(a => a.StepOrder).FirstOrDefault())
            .ToListAsync(ct);

        var lastServiceMap = latestActions
            .Where(a => a != null)
            .ToDictionary(a => a!.FlowId, a => a!.ServiceName);

        // Load distinct services per order code group
        var flowIdToOrderCode = groups.ToDictionary(g => g.RepresentativeFlowId, g => g.OrderCode);
        var allFlows = await db.LogFlows
            .AsNoTracking()
            .Where(f => representativeFlowIds.Contains(f.FlowId))
            .ToListAsync(ct);

        // Build flowIds by orderCode map
        var orderCodeFlowIds = allFlows
            .GroupBy(f => f.OrderCode!)
            .ToDictionary(g => g.Key, g => g.Select(f => f.FlowId).ToList());

        var serviceNamesByOrderCode = new Dictionary<string, List<string>>();
        foreach (var kvp in orderCodeFlowIds)
        {
            var services = await db.LogActions
                .AsNoTracking()
                .Where(a => kvp.Value.Contains(a.FlowId))
                .Select(a => a.ServiceName)
                .Distinct()
                .ToListAsync(ct);
            serviceNamesByOrderCode[kvp.Key] = services;
        }

        var items = groups.Select(g =>
        {
            var overallStatus = ComputeOverallStatus(g.AllStatuses);
            lastServiceMap.TryGetValue(g.RepresentativeFlowId, out var lastServiceName);
            serviceNamesByOrderCode.TryGetValue(g.OrderCode, out var services);

            return new BusinessFlowSummaryDto(
                g.OrderCode,
                g.RepresentativeFlowId,
                g.UserEmail,
                g.CustomerEmail,
                g.CustomerPhone,
                g.PartnerId,
                g.PaymentId,
                g.TransactionId,
                overallStatus,
                services ?? new List<string>(),
                g.ActionCount,
                g.FailedCount,
                g.SuccessCount,
                g.FirstSeenAt,
                g.LastSeenAt,
                g.LastMessage,
                lastServiceName,
                g.LastActionType);
        }).ToList();

        return new PagedResponse<BusinessFlowSummaryDto>(items, page, pageSize, totalItems, totalPages);
    }

    /// <inheritdoc />
    public async Task<BusinessFlowDetailDto?> GetByOrderCodeAsync(
        string orderCode,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var flows = await db.LogFlows
            .AsNoTracking()
            .Where(f => f.OrderCode == orderCode)
            .ToListAsync(ct);

        if (flows.Count == 0)
        {
            return null;
        }

        var flowIds = flows.Select(f => f.FlowId).ToList();

        var allActions = await db.LogActions
            .AsNoTracking()
            .Where(a => flowIds.Contains(a.FlowId))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        // Build a representative summary from the most recent flow
        var latestFlow = flows
            .OrderByDescending(f => f.UpdatedAt)
            .ThenByDescending(f => f.CreatedAt)
            .First();

        var serviceNames = allActions
            .Select(a => a.ServiceName)
            .Distinct()
            .ToList();

        var allStatuses = flows.Select(f => f.Status).ToList();
        var overallStatus = ComputeOverallStatus(allStatuses);

        var totalActionCount = flows.Sum(f => f.TotalSteps);
        var totalFailedCount = flows.Sum(f => f.FailedSteps);
        var totalSuccessCount = flows.Sum(f => f.SuccessSteps);

        // Get last service name from the latest action of the latest flow
        var lastActionOfLatestFlow = allActions
            .Where(a => a.FlowId == latestFlow.FlowId)
            .OrderByDescending(a => a.StepOrder)
            .FirstOrDefault();

        var summary = new BusinessFlowSummaryDto(
            orderCode,
            latestFlow.FlowId,
            flows.Select(f => f.UserEmail).FirstOrDefault(e => e != null),
            flows.Select(f => f.CustomerEmail).FirstOrDefault(e => e != null),
            flows.Select(f => f.CustomerPhone).FirstOrDefault(e => e != null),
            flows.Select(f => f.PartnerId).FirstOrDefault(e => e != null),
            flows.Select(f => f.PaymentId).FirstOrDefault(e => e != null),
            flows.Select(f => f.TransactionId).FirstOrDefault(e => e != null),
            overallStatus,
            serviceNames,
            totalActionCount,
            totalFailedCount,
            totalSuccessCount,
            flows.Min(f => f.CreatedAt),
            flows.Max(f => f.UpdatedAt),
            latestFlow.LastMessage,
            lastActionOfLatestFlow?.ServiceName,
            latestFlow.LastActionType);

        var timeline = allActions.Select(a => new BusinessFlowActionDto(
            a.FlowId,
            a.EventId,
            a.ServiceName,
            a.ActionType,
            a.Status,
            a.Message,
            a.ErrorCode,
            a.ErrorMessage,
            a.DurationMs,
            a.CorrelationId,
            a.CreatedAt,
            a.RequestTime,
            a.ResponseTime,
            null, // payloads loaded lazily via action detail endpoint
            null,
            null,
            null))
            .ToList();

        return new BusinessFlowDetailDto(summary, timeline);
    }

    private static IQueryable<LogFlow> ApplyKeywordSearch(
        IQueryable<LogFlow> query,
        LoggerDbContext db,
        string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return query;
        }

        var pattern = $"%{keyword}%";
        var isNpgsql = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        if (isNpgsql)
        {
            return query.Where(f =>
                (f.OrderCode != null && EF.Functions.ILike(f.OrderCode, pattern)) ||
                (f.PaymentId != null && EF.Functions.ILike(f.PaymentId, pattern)) ||
                (f.TransactionId != null && EF.Functions.ILike(f.TransactionId, pattern)) ||
                (f.UserEmail != null && EF.Functions.ILike(f.UserEmail, pattern)) ||
                (f.CustomerEmail != null && EF.Functions.ILike(f.CustomerEmail, pattern)) ||
                (f.CustomerPhone != null && EF.Functions.ILike(f.CustomerPhone, pattern)) ||
                (f.LastMessage != null && EF.Functions.ILike(f.LastMessage, pattern)));
        }

        var lower = keyword.ToLowerInvariant();
        return query.Where(f =>
            (f.OrderCode != null && f.OrderCode.ToLower().Contains(lower)) ||
            (f.PaymentId != null && f.PaymentId.ToLower().Contains(lower)) ||
            (f.TransactionId != null && f.TransactionId.ToLower().Contains(lower)) ||
            (f.UserEmail != null && f.UserEmail.ToLower().Contains(lower)) ||
            (f.CustomerEmail != null && f.CustomerEmail.ToLower().Contains(lower)) ||
            (f.CustomerPhone != null && f.CustomerPhone.ToLower().Contains(lower)) ||
            (f.LastMessage != null && f.LastMessage.ToLower().Contains(lower)));
    }

    private static string ComputeOverallStatus(List<string?> statuses)
    {
        if (statuses.Any(s => s == StatusTypes.Failed))
        {
            return StatusTypes.Failed;
        }

        if (statuses.All(s => s == StatusTypes.Success))
        {
            return StatusTypes.Success;
        }

        if (statuses.Any(s => s == StatusTypes.PartialFailed))
        {
            return StatusTypes.PartialFailed;
        }

        if (statuses.Any(s => s == StatusTypes.Running))
        {
            return StatusTypes.Running;
        }

        return "UNKNOWN";
    }
}
