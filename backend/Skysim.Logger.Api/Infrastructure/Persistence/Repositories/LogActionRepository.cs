using Microsoft.EntityFrameworkCore;
using Npgsql;
using Skysim.Logger.Api.Domain.Entities;
using Skysim.Logger.Api.Domain.Enums;
using Status = Skysim.Logger.Api.Domain.Enums.Status;

namespace Skysim.Logger.Api.Infrastructure.Persistence.Repositories;

public class LogActionRepository : ILogActionRepository
{
    private readonly LoggerDbContext _db;

    public LogActionRepository(LoggerDbContext db)
    {
        _db = db;
    }

    public async Task<LogAction> InsertAsync(
        LogAction action,
        CancellationToken cancellationToken = default)
    {
        // Derive step_order from existing rows for this flow_id
        var stepOrder = await GetNextStepOrderAsync(action.FlowId, cancellationToken);

        action.StepOrder = stepOrder;
        action.CreatedAt = DateTime.UtcNow;
        action.UpdatedAt = DateTime.UtcNow;

        _db.LogActions.Add(action);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateEventId(ex))
        {
            throw new Exceptions.DuplicateEventException(action.EventId, ex);
        }

        return action;
    }

    private async Task<int> GetNextStepOrderAsync(string flowId, CancellationToken cancellationToken)
    {
        var count = await _db.LogActions
            .CountAsync(a => a.FlowId == flowId, cancellationToken);

        return count + 1;
    }

    private static bool IsDuplicateEventId(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is not PostgresException pgEx) return false;
        return pgEx.SqlState == "23505"; // unique_violation
    }
}
