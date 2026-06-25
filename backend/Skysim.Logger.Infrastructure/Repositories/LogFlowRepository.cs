using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Infrastructure.Repositories;

public class LogFlowRepository : ILogFlowRepository
{
    private readonly LoggerDbContext _context;

    public LogFlowRepository(LoggerDbContext context)
    {
        _context = context;
    }

    public async Task<LogFlow?> GetByFlowIdAsync(string flowId, CancellationToken cancellationToken = default)
    {
        return await _context.LogFlows
            .Include(f => f.Actions.OrderBy(a => a.StepOrder))
            .FirstOrDefaultAsync(f => f.FlowId == flowId, cancellationToken);
    }

    public async Task<LogFlow> UpsertAsync(
        string flowId,
        Action<LogFlow> updateFlow,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.LogFlows
            .FirstOrDefaultAsync(f => f.FlowId == flowId, cancellationToken);

        if (existing == null)
        {
            var newFlow = new LogFlow
            {
                Id = Guid.NewGuid(),
                FlowId = flowId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            updateFlow(newFlow);
            _context.LogFlows.Add(newFlow);
            await _context.SaveChangesAsync(cancellationToken);
            return newFlow;
        }
        else
        {
            updateFlow(existing);
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }
    }

    public async Task<bool> ExistsAsync(string flowId, CancellationToken cancellationToken = default)
    {
        return await _context.LogFlows.AnyAsync(f => f.FlowId == flowId, cancellationToken);
    }
}
