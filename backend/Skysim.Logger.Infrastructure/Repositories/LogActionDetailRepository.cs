using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;

namespace Skysim.Logger.Infrastructure.Repositories;

public class LogActionDetailRepository : ILogActionDetailRepository
{
    private readonly LoggerDbContext _context;

    public LogActionDetailRepository(LoggerDbContext context)
    {
        _context = context;
    }

    public async Task<LogActionDetail> UpsertAsync(LogActionDetail detail, CancellationToken cancellationToken = default)
    {
        var existing = await _context.LogActionDetails
            .FirstOrDefaultAsync(d => d.ActionId == detail.ActionId, cancellationToken);

        if (existing == null)
        {
            detail.Id = Guid.NewGuid();
            detail.CreatedAt = DateTime.UtcNow;
            detail.UpdatedAt = DateTime.UtcNow;
            _context.LogActionDetails.Add(detail);
        }
        else
        {
            existing.RequestPayload = detail.RequestPayload;
            existing.ResponsePayload = detail.ResponsePayload;
            existing.ErrorPayload = detail.ErrorPayload;
            existing.Metadata = detail.Metadata;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return existing ?? detail;
    }

    public async Task<LogActionDetail?> GetByActionIdAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        return await _context.LogActionDetails
            .FirstOrDefaultAsync(d => d.ActionId == actionId, cancellationToken);
    }
}
