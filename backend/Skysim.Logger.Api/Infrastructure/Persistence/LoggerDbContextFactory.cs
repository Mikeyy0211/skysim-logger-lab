using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Skysim.Logger.Api.Infrastructure.Persistence;

public class LoggerDbContextFactory : IDesignTimeDbContextFactory<LoggerDbContext>
{
    public LoggerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LoggerDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("SKYSIM_LOGGER_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=skysim_logger";

        optionsBuilder.UseNpgsql(connectionString);

        return new LoggerDbContext(optionsBuilder.Options);
    }
}
