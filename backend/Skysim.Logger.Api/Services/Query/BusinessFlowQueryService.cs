using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Contracts.Constants;
using Skysim.Logger.Contracts.DTOs;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Api.Services.Query;

/// <summary>
/// Read model query service for business flows. Database rows remain technical flows;
/// grouping by order code only happens while building the response.
/// </summary>
public class BusinessFlowQueryService : IBusinessFlowQueryService
{
    private static readonly string[] TechnicalNoiseOrderCodes =
    [
        "WAITING_ONEPAY",
        "ROBOTS.TXT-SERVICE",
        "UNKNOWN-SERVICE",
        "ADMIN OVERVIEW",
        "OTP REQUEST",
        "ANNOUNCEMENT/LOAD",
        "OVERVIEW/LOAD",
        "/",
        "/ROBOTS.TXT"
    ];

    private readonly IDbContextFactory<LoggerDbContext> _dbContextFactory;

    public BusinessFlowQueryService(IDbContextFactory<LoggerDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<BusinessDashboardDto> GetDashboardSummaryAsync(
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var validOrderFlows = ApplyValidOrderCodeFilter(db.LogFlows.AsNoTracking());

        // Group and resolve the business status in the API read model before counting or
        // selecting recent rows. The frontend never receives technical-flow records for KPI work.
        var groupedRows = await validOrderFlows
            .GroupBy(f => f.OrderCode!)
            .Select(g => new
            {
                OrderCode = g.Key,
                FirstSeen = g.Min(f => f.CreatedAt),
                LastSeen = g.Max(f => f.UpdatedAt),
                HasFailed = g.Any(f => f.Status == StatusTypes.Failed),
                HasSuccess = g.Any(f => f.Status == StatusTypes.Success),
                HasPartialFailed = g.Any(f => f.Status == StatusTypes.PartialFailed),
                HasRunning = g.Any(f =>
                    f.Status == StatusTypes.Running ||
                    f.Status == StatusTypes.InProgress ||
                    f.Status == "PROCESSING")
            })
            .ToListAsync(ct);

        var orders = groupedRows
            .Select(row => new BusinessOrderAggregate(
                row.OrderCode,
                ComputeOverallStatus(
                    row.HasFailed,
                    row.HasSuccess,
                    row.HasPartialFailed,
                    row.HasRunning),
                row.FirstSeen,
                row.LastSeen))
            .ToList();

        var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var completedAndAttentionCount = orders.Count(order =>
            order.Status == StatusTypes.Success ||
            order.Status == StatusTypes.Failed ||
            order.Status == StatusTypes.PartialFailed);
        var completedOrders = orders.LongCount(order => order.Status == StatusTypes.Success);
        // Completion rate = SUCCESS / (SUCCESS + FAILED + PARTIAL_FAILED) * 100;
        // RUNNING/PROCESSING orders are intentionally excluded from the denominator.
        var completionRate = completedAndAttentionCount == 0
            ? 0d
            : Math.Round((double)completedOrders / completedAndAttentionCount * 100, 2);

        var recentOrderCodes = orders
            .Where(order => order.Status == StatusTypes.Failed || order.Status == StatusTypes.PartialFailed)
            .OrderBy(order => order.Status == StatusTypes.Failed ? 0 : 1)
            .ThenByDescending(order => order.LastSeen)
            .Take(5)
            .Select(order => order.OrderCode)
            .Concat(
                orders
                    .Where(order => order.Status == StatusTypes.Success)
                    .OrderByDescending(order => order.LastSeen)
                    .Take(5)
                    .Select(order => order.OrderCode))
            .Distinct()
            .ToList();

        var recentFlows = recentOrderCodes.Count == 0
            ? []
            : await db.LogFlows
                .AsNoTracking()
                .Where(flow => recentOrderCodes.Contains(flow.OrderCode!))
                .ToListAsync(ct);
        var recentFlowIds = recentFlows.Select(flow => flow.FlowId).ToList();
        var recentActions = recentFlowIds.Count == 0
            ? []
            : await db.LogActions
                .AsNoTracking()
                .Where(action => recentFlowIds.Contains(action.FlowId))
                .ToListAsync(ct);

        var flowsByOrder = recentFlows
            .GroupBy(flow => flow.OrderCode!)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LogFlow>)group.ToList());
        var actionsByOrder = recentActions
            .Join(
                recentFlows,
                action => action.FlowId,
                flow => flow.FlowId,
                (action, flow) => new { action, flow.OrderCode })
            .Where(item => item.OrderCode != null)
            .GroupBy(item => item.OrderCode!)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<LogAction>)group.Select(item => item.action).ToList());

        var orderDetails = new Dictionary<string, BusinessDashboardOrderDto>();
        foreach (var order in orders.Where(order => recentOrderCodes.Contains(order.OrderCode)))
        {
            flowsByOrder.TryGetValue(order.OrderCode, out var flows);
            actionsByOrder.TryGetValue(order.OrderCode, out var actions);
            orderDetails[order.OrderCode] = CreateDashboardOrder(
                order,
                flows ?? [],
                actions ?? []);
        }

        var statusCounts = await db.LogFlows
            .AsNoTracking()
            .GroupBy(flow => flow.Status)
            .Select(group => new TechnicalStatusCount(group.Key, group.LongCount()))
            .ToListAsync(ct);

        var technicalSuccess = CountStatus(statusCounts, StatusTypes.Success);
        var technicalFailed = CountStatus(statusCounts, StatusTypes.Failed);
        var technicalRunning = CountStatus(statusCounts, StatusTypes.Running) +
            CountStatus(statusCounts, StatusTypes.InProgress) +
            CountStatus(statusCounts, "PROCESSING");
        var technicalPartial = CountStatus(statusCounts, StatusTypes.PartialFailed);
        var technicalRecognizedTotal = technicalSuccess + technicalFailed + technicalRunning + technicalPartial;
        var technicalSummary = new TechnicalDashboardSummaryDto(
            statusCounts.Sum(row => row.Count),
            await db.LogActions.AsNoTracking().LongCountAsync(ct),
            await db.LogFlows.AsNoTracking().CountAsync(flow => flow.CreatedAt >= todayUtc, ct),
            technicalFailed,
            technicalRecognizedTotal == 0
                ? 0d
                : Math.Round((double)technicalSuccess / technicalRecognizedTotal * 100, 2));

        return new BusinessDashboardDto(
            orders.Count,
            orders.LongCount(order => order.FirstSeen >= todayUtc || order.LastSeen >= todayUtc),
            orders.LongCount(order => order.Status == StatusTypes.Running),
            orders.LongCount(order => order.Status == StatusTypes.Failed || order.Status == StatusTypes.PartialFailed),
            completedOrders,
            completionRate,
            orders
                .Where(order => order.Status == StatusTypes.Failed || order.Status == StatusTypes.PartialFailed)
                .OrderBy(order => order.Status == StatusTypes.Failed ? 0 : 1)
                .ThenByDescending(order => order.LastSeen)
                .Take(5)
                .Select(order => orderDetails[order.OrderCode])
                .ToList(),
            orders
                .Where(order => order.Status == StatusTypes.Success)
                .OrderByDescending(order => order.LastSeen)
                .Take(5)
                .Select(order => orderDetails[order.OrderCode])
                .ToList(),
            technicalSummary);
    }

    public async Task<PagedResponse<BusinessFlowSummaryDto>> GetListAsync(
        BusinessFlowListQuery query,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = ApplyValidOrderCodeFilter(db.LogFlows.AsNoTracking());

        baseQuery = ApplyKeywordSearch(baseQuery, db, query.Keyword);
        baseQuery = ApplyDateFilters(baseQuery, query);

        // Group before counting and paging. This is intentionally a grouped read model,
        // not a persistence change and not a technical-flow pagination workaround.
        var groupedRows = await baseQuery
            .GroupBy(f => f.OrderCode!)
            .Select(g => new
            {
                OrderCode = g.Key,
                RepresentativeFlowId = g.OrderByDescending(f => f.UpdatedAt)
                    .ThenByDescending(f => f.CreatedAt)
                    .Select(f => f.FlowId)
                    .First(),
                UserId = g.Where(f => f.UserId != null)
                    .OrderByDescending(f => f.UpdatedAt)
                    .ThenByDescending(f => f.CreatedAt)
                    .Select(f => f.UserId)
                    .FirstOrDefault(),
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
                HasFailed = g.Any(f => f.Status == StatusTypes.Failed),
                HasSuccess = g.Any(f => f.Status == StatusTypes.Success),
                HasPartialFailed = g.Any(f => f.Status == StatusTypes.PartialFailed),
                HasRunning = g.Any(f => f.Status == StatusTypes.Running || f.Status == StatusTypes.InProgress),
                TechnicalFlowCount = g.Count()
            })
            .ToListAsync(ct);

        var statusFilter = NormalizeStatus(query.Status);
        var filteredRows = groupedRows
            .Select(row => new
            {
                Row = row,
                OverallStatus = ComputeOverallStatus(
                    row.HasFailed,
                    row.HasSuccess,
                    row.HasPartialFailed,
                    row.HasRunning)
            })
            .Where(x => statusFilter == null || x.OverallStatus == statusFilter)
            .ToList();

        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        filteredRows = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "ordercode" => descending
                ? filteredRows.OrderByDescending(x => x.Row.OrderCode).ToList()
                : filteredRows.OrderBy(x => x.Row.OrderCode).ToList(),
            "status" => descending
                ? filteredRows.OrderByDescending(x => x.OverallStatus).ThenByDescending(x => x.Row.LastSeenAt).ToList()
                : filteredRows.OrderBy(x => x.OverallStatus).ThenByDescending(x => x.Row.LastSeenAt).ToList(),
            "firstseen" or "firstseenat" => descending
                ? filteredRows.OrderByDescending(x => x.Row.FirstSeenAt).ToList()
                : filteredRows.OrderBy(x => x.Row.FirstSeenAt).ToList(),
            _ => descending
                ? filteredRows.OrderByDescending(x => x.Row.LastSeenAt).ToList()
                : filteredRows.OrderBy(x => x.Row.LastSeenAt).ToList()
        };

        var totalItems = filteredRows.Count;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var pageRows = filteredRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        if (pageRows.Count == 0)
        {
            return new PagedResponse<BusinessFlowSummaryDto>(
                [], page, pageSize, totalItems, totalPages);
        }

        // Load all technical flows/actions for the selected order groups. This fixes the
        // previous representative-flow-only service calculation while preserving every row.
        var selectedOrderCodes = pageRows.Select(x => x.Row.OrderCode).ToList();
        var selectedFlows = await db.LogFlows
            .AsNoTracking()
            .Where(f => selectedOrderCodes.Contains(f.OrderCode!))
            .ToListAsync(ct);
        var selectedFlowIds = selectedFlows.Select(f => f.FlowId).ToList();
        var selectedActions = selectedFlowIds.Count == 0
            ? []
            : await db.LogActions
                .AsNoTracking()
                .Where(a => selectedFlowIds.Contains(a.FlowId))
                .ToListAsync(ct);

        var flowsByOrder = selectedFlows
            .GroupBy(f => f.OrderCode!)
            .ToDictionary(g => g.Key, g => g.ToList());
        var actionsByOrder = selectedActions
            .Join(
                selectedFlows,
                action => action.FlowId,
                flow => flow.FlowId,
                (action, flow) => new { action, orderCode = flow.OrderCode! })
            .GroupBy(x => x.orderCode)
            .ToDictionary(g => g.Key, g => g.Select(x => x.action).ToList());

        var items = pageRows.Select(x =>
        {
            var row = x.Row;
            flowsByOrder.TryGetValue(row.OrderCode, out var flows);
            actionsByOrder.TryGetValue(row.OrderCode, out var actions);
            flows ??= [];
            actions ??= [];

            var latestAction = LatestAction(actions);
            var latestFailedAction = actions
                .Where(IsFailedAction)
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.StepOrder)
                .FirstOrDefault();

            var overallStatus = ComputeOverallStatus(flows);
            var actionCount = actions.Count > 0 ? actions.Count : row.ActionCount;
            var successCount = actions.Count > 0
                ? actions.Count(a => a.Status == StatusTypes.Success)
                : row.SuccessCount;
            var failedCount = actions.Count > 0
                ? actions.Count(IsFailedAction)
                : row.FailedCount;

            return new BusinessFlowSummaryDto(
                row.OrderCode,
                row.RepresentativeFlowId,
                FirstNonEmpty(flows.Select(f => f.UserId)) ?? row.UserId,
                FirstNonEmpty(flows.Select(f => f.UserEmail)) ?? row.UserEmail,
                FirstNonEmpty(flows.Select(f => f.CustomerEmail)) ?? row.CustomerEmail,
                FirstNonEmpty(flows.Select(f => f.CustomerPhone)) ?? row.CustomerPhone,
                FirstNonEmpty(flows.Select(f => f.PartnerId)) ?? row.PartnerId,
                FirstNonEmpty(flows.Select(f => f.PaymentId)) ?? row.PaymentId,
                FirstNonEmpty(flows.Select(f => f.TransactionId)) ?? row.TransactionId,
                overallStatus,
                actions.Select(a => a.ServiceName)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList(),
                actionCount,
                failedCount,
                successCount,
                flows.Count > 0 ? flows.Min(f => f.CreatedAt) : row.FirstSeenAt,
                flows.Count > 0 ? flows.Max(f => f.UpdatedAt) : row.LastSeenAt,
                latestAction?.Message ?? row.LastMessage,
                latestAction?.ServiceName,
                latestAction?.ActionType ?? row.LastActionType,
                latestFailedAction?.ActionType,
                latestFailedAction?.Message,
                IssueSummary(latestFailedAction),
                flows.Count > 0 ? flows.Count : row.TechnicalFlowCount);
        }).ToList();

        return new PagedResponse<BusinessFlowSummaryDto>(items, page, pageSize, totalItems, totalPages);
    }

    public async Task<BusinessFlowDetailDto?> GetByOrderCodeAsync(
        string orderCode,
        CancellationToken ct = default)
    {
        if (IsTechnicalNoiseOrderCode(orderCode))
        {
            return null;
        }

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
        var actions = await db.LogActions
            .AsNoTracking()
            .Where(a => flowIds.Contains(a.FlowId))
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.StepOrder)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

        var latestFlow = flows
            .OrderByDescending(f => f.UpdatedAt)
            .ThenByDescending(f => f.CreatedAt)
            .First();
        var latestAction = LatestAction(actions);
        var latestFailedAction = actions
            .Where(IsFailedAction)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.StepOrder)
            .FirstOrDefault();

        var summary = new BusinessFlowSummaryDto(
            orderCode,
            latestFlow.FlowId,
            FirstNonEmpty(flows.Select(f => f.UserId)),
            FirstNonEmpty(flows.Select(f => f.UserEmail)),
            FirstNonEmpty(flows.Select(f => f.CustomerEmail)),
            FirstNonEmpty(flows.Select(f => f.CustomerPhone)),
            FirstNonEmpty(flows.Select(f => f.PartnerId)),
            FirstNonEmpty(flows.Select(f => f.PaymentId)),
            FirstNonEmpty(flows.Select(f => f.TransactionId)),
            ComputeOverallStatus(flows),
            actions.Select(a => a.ServiceName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList(),
            actions.Count > 0 ? actions.Count : flows.Sum(f => f.TotalSteps),
            actions.Count > 0 ? actions.Count(IsFailedAction) : flows.Sum(f => f.FailedSteps),
            actions.Count > 0 ? actions.Count(a => a.Status == StatusTypes.Success) : flows.Sum(f => f.SuccessSteps),
            flows.Min(f => f.CreatedAt),
            flows.Max(f => f.UpdatedAt),
            latestAction?.Message ?? latestFlow.LastMessage,
            latestAction?.ServiceName,
            latestAction?.ActionType ?? latestFlow.LastActionType,
            latestFailedAction?.ActionType,
            latestFailedAction?.Message,
            IssueSummary(latestFailedAction),
            flows.Count);

        var timeline = actions.Select(a => new BusinessFlowActionDto(
            a.Id,
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
            null,
            null,
            null,
            null)).ToList();

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

        var pattern = $"%{keyword.Trim()}%";
        var isNpgsql = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

        if (isNpgsql)
        {
            return query.Where(f =>
                (f.OrderCode != null && EF.Functions.ILike(f.OrderCode, pattern)) ||
                (f.PaymentId != null && EF.Functions.ILike(f.PaymentId, pattern)) ||
                (f.TransactionId != null && EF.Functions.ILike(f.TransactionId, pattern)) ||
                (f.UserEmail != null && EF.Functions.ILike(f.UserEmail, pattern)) ||
                (f.CustomerEmail != null && EF.Functions.ILike(f.CustomerEmail, pattern)) ||
                (f.CustomerPhone != null && EF.Functions.ILike(f.CustomerPhone, pattern)));
        }

        var lower = keyword.Trim().ToLowerInvariant();
        return query.Where(f =>
            (f.OrderCode != null && f.OrderCode.ToLower().Contains(lower)) ||
            (f.PaymentId != null && f.PaymentId.ToLower().Contains(lower)) ||
            (f.TransactionId != null && f.TransactionId.ToLower().Contains(lower)) ||
            (f.UserEmail != null && f.UserEmail.ToLower().Contains(lower)) ||
            (f.CustomerEmail != null && f.CustomerEmail.ToLower().Contains(lower)) ||
            (f.CustomerPhone != null && f.CustomerPhone.ToLower().Contains(lower)));
    }

    private static IQueryable<LogFlow> ApplyValidOrderCodeFilter(IQueryable<LogFlow> query) =>
        query.Where(flow =>
            flow.OrderCode != null &&
            flow.OrderCode != "" &&
            !TechnicalNoiseOrderCodes.Contains(flow.OrderCode.ToUpper()) &&
            !flow.OrderCode.ToUpper().StartsWith("GET "));

    private static bool IsTechnicalNoiseOrderCode(string orderCode)
    {
        var normalized = orderCode.Trim().ToUpperInvariant();
        return TechnicalNoiseOrderCodes.Contains(normalized) || normalized.StartsWith("GET ");
    }

    private static IQueryable<LogFlow> ApplyDateFilters(
        IQueryable<LogFlow> query,
        BusinessFlowListQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.FromDate) &&
            DateTime.TryParse(request.FromDate, out var fromDate))
        {
            var utcFrom = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
            query = query.Where(f => f.CreatedAt >= utcFrom);
        }

        if (!string.IsNullOrWhiteSpace(request.ToDate) &&
            DateTime.TryParse(request.ToDate, out var toDate))
        {
            var utcTo = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(f => f.CreatedAt <= utcTo);
        }

        return query;
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToUpperInvariant() switch
        {
            StatusTypes.Success => StatusTypes.Success,
            StatusTypes.Failed => StatusTypes.Failed,
            StatusTypes.PartialFailed => StatusTypes.PartialFailed,
            StatusTypes.Running or StatusTypes.InProgress or "PROCESSING" => StatusTypes.Running,
            _ => status.Trim().ToUpperInvariant()
        };
    }

    private static string ComputeOverallStatus(IReadOnlyList<LogFlow> flows)
    {
        var hasFailed = flows.Any(f => f.Status == StatusTypes.Failed);
        var hasSuccess = flows.Any(f => f.Status == StatusTypes.Success);
        var hasPartial = flows.Any(f => f.Status == StatusTypes.PartialFailed);
        var hasRunning = flows.Any(f =>
            f.Status == StatusTypes.Running ||
            f.Status == StatusTypes.InProgress ||
            f.Status == "PROCESSING");
        return ComputeOverallStatus(hasFailed, hasSuccess, hasPartial, hasRunning);
    }

    private static string ComputeOverallStatus(
        bool hasFailed,
        bool hasSuccess,
        bool hasPartial,
        bool hasRunning)
    {
        if (hasPartial || (hasFailed && (hasSuccess || hasRunning)))
        {
            return StatusTypes.PartialFailed;
        }

        if (hasFailed)
        {
            return StatusTypes.Failed;
        }

        if (hasRunning)
        {
            return StatusTypes.Running;
        }

        if (hasSuccess)
        {
            return StatusTypes.Success;
        }

        return "UNKNOWN";
    }

    private static long CountStatus(IEnumerable<TechnicalStatusCount> rows, string status) =>
        rows.FirstOrDefault(row => row.Status == status)?.Count ?? 0;

    private static BusinessDashboardOrderDto CreateDashboardOrder(
        BusinessOrderAggregate order,
        IReadOnlyList<LogFlow> flows,
        IReadOnlyList<LogAction> actions)
    {
        var latestFlow = flows
            .OrderByDescending(flow => flow.UpdatedAt)
            .ThenByDescending(flow => flow.CreatedAt)
            .FirstOrDefault();
        var latestAction = LatestAction(actions);
        var latestFailedAction = actions
            .Where(IsFailedAction)
            .OrderByDescending(action => action.CreatedAt)
            .ThenByDescending(action => action.StepOrder)
            .ThenByDescending(action => action.Id)
            .FirstOrDefault();

        var failedFlowMessage = flows
            .Where(flow => flow.Status == StatusTypes.Failed || flow.Status == StatusTypes.PartialFailed)
            .OrderByDescending(flow => flow.UpdatedAt)
            .Select(flow => flow.LastMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        return new BusinessDashboardOrderDto(
            order.OrderCode,
            FirstNonEmpty(flows.Select(flow => flow.UserEmail)),
            FirstNonEmpty(flows.Select(flow => flow.CustomerEmail)),
            FirstNonEmpty(flows.Select(flow => flow.CustomerPhone)),
            FirstNonEmpty(flows.Select(flow => flow.PaymentId)),
            FirstNonEmpty(flows.Select(flow => flow.TransactionId)),
            order.Status,
            latestAction?.ActionType ?? latestFlow?.LastActionType,
            latestAction?.Message ?? latestFlow?.LastMessage,
            latestFailedAction?.ActionType,
            latestFailedAction?.Message,
            IssueSummary(latestFailedAction) ?? failedFlowMessage,
            flows.Count == 0 ? order.FirstSeen : flows.Min(flow => flow.CreatedAt),
            flows.Count == 0 ? order.LastSeen : flows.Max(flow => flow.UpdatedAt),
            actions.Count > 0 ? actions.Count : flows.Sum(flow => flow.TotalSteps),
            actions.Count > 0 ? actions.Count(IsFailedAction) : flows.Sum(flow => flow.FailedSteps),
            flows.Count);
    }

    private sealed record BusinessOrderAggregate(
        string OrderCode,
        string Status,
        DateTime FirstSeen,
        DateTime LastSeen);

    private sealed record TechnicalStatusCount(string Status, long Count);

    private static LogAction? LatestAction(IEnumerable<LogAction> actions) => actions
        .OrderByDescending(a => a.CreatedAt)
        .ThenByDescending(a => a.StepOrder)
        .ThenByDescending(a => a.Id)
        .FirstOrDefault();

    private static bool IsFailedAction(LogAction action) =>
        action.Status.Equals(StatusTypes.Failed, StringComparison.OrdinalIgnoreCase) ||
        action.Status.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ||
        action.Status.Equals("TIMEOUT", StringComparison.OrdinalIgnoreCase);

    private static string? IssueSummary(LogAction? action)
    {
        if (action == null)
        {
            return null;
        }

        return FirstNonEmpty([action.ErrorMessage, action.Message, action.ErrorCode]);
    }

    private static string? FirstNonEmpty(IEnumerable<string?> values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
