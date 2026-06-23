using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Skysim.Logger.Api.Infrastructure.Persistence;

public class LoggerDbContextFactory : IDesignTimeDbContextFactory<LoggerDbContext>
{
    public LoggerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LoggerDbContext>();

        var connectionString = "Host=localhost;Port=5432;Database=skysim_logger;Username=skysim;Password=skysim_password";
        optionsBuilder.UseNpgsql(connectionString);

        return new LoggerDbContext(optionsBuilder.Options);
    }
}
