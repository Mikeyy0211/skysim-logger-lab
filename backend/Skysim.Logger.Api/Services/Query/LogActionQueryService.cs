using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;
using Skysim.Logger.Api.Infrastructure.Persistence;

namespace Skysim.Logger.Api.Services.Query;

public class LogActionQueryService : ILogActionQueryService
{
    private readonly IDbContextFactory<LoggerDbContext> _dbContextFactory;
    private readonly SensitiveDataMasker _masker;

    public LogActionQueryService(
        IDbContextFactory<LoggerDbContext> dbContextFactory,
        SensitiveDataMasker masker)
    {
        _dbContextFactory = dbContextFactory;
        _masker = masker;
    }

    public async Task<PagedResponse<LogActionDto>> GetByFlowIdAsync(
        LogActionListQuery query,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.LogActions
            .AsNoTracking()
            .Where(a => a.FlowId == query.FlowId);

        if (!string.IsNullOrWhiteSpace(query.ServiceName))
        {
            q = q.Where(a => a.ServiceName == query.ServiceName);
        }

        var totalItems = await q.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await q
            .OrderBy(a => a.StepOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return new PagedResponse<LogActionDto>(items, page, pageSize, totalItems, totalPages);
    }

    public async Task<LogActionDetailsDto?> GetDetailsAsync(
        Guid actionId,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var action = await db.LogActions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == actionId, ct);

        if (action == null)
        {
            return null;
        }

        var detail = await db.LogActionDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ActionId == actionId, ct);

        var actionDto = new LogActionDto(
            action.Id,
            action.EventId,
            action.FlowId,
            action.StepOrder,
            action.ServiceName,
            action.ActionType,
            action.Status,
            action.Message,
            action.ErrorCode,
            action.ErrorMessage,
            action.RequestTime,
            action.ResponseTime,
            action.DurationMs,
            action.CorrelationId,
            action.CreatedAt);

        string? MaskPayload(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return _masker.MaskJson(json);
            }
            catch
            {
                return json;
            }
        }

        return new LogActionDetailsDto(
            actionDto,
            MaskPayload(detail?.RequestPayload),
            MaskPayload(detail?.ResponsePayload),
            MaskPayload(detail?.ErrorPayload),
            MaskPayload(detail?.Metadata));
    }
}
