using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Infrastructure.Data;

namespace Skysim.Logger.Infrastructure.Repositories;

public class LoggerDbContextFactory : Microsoft.EntityFrameworkCore.IDbContextFactory<LoggerDbContext>
{
    private readonly DbContextOptions<LoggerDbContext> _options;

    public LoggerDbContextFactory(DbContextOptions<LoggerDbContext> options)
    {
        _options = options;
    }

    public LoggerDbContext CreateDbContext()
    {
        return new LoggerDbContext(_options);
    }
}
