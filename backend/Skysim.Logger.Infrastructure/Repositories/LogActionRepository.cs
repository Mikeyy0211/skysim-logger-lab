using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Infrastructure.Repositories;

public class LogActionRepository : ILogActionRepository
{
    private readonly LoggerDbContext _context;

    public LogActionRepository(LoggerDbContext context)
    {
        _context = context;
    }

    public async Task<LogAction> InsertAsync(LogAction action, CancellationToken cancellationToken = default)
    {
        action.Id = Guid.NewGuid();
        action.CreatedAt = DateTime.UtcNow;
        action.UpdatedAt = DateTime.UtcNow;

        if (action.StepOrder == 0)
        {
            var maxStepOrder = await _context.LogActions
                .Where(a => a.FlowId == action.FlowId)
                .MaxAsync(a => (int?)a.StepOrder, cancellationToken) ?? 0;
            action.StepOrder = maxStepOrder + 1;
        }

        _context.LogActions.Add(action);
        await _context.SaveChangesAsync(cancellationToken);

        return action;
    }

    public async Task<LogAction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.LogActions
            .Include(a => a.Detail)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<LogAction?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.LogActions
            .FirstOrDefaultAsync(a => a.EventId == eventId, cancellationToken);
    }
}
