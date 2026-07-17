using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PXA.Infrastructure.Persistence;

public sealed class PxaDbContextFactory : IDesignTimeDbContextFactory<PxaDbContext>
{
    private const string DefaultDevelopmentConnection =
        "Host=localhost;Port=5432;Database=pxa;Username=pxa;Password=pxa-local";

    public PxaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PXA_DATABASE_CONNECTION")
            ?? DefaultDevelopmentConnection;

        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseNpgsql(connectionString, postgres =>
                postgres.MigrationsHistoryTable("__ef_migrations_history", DatabaseSchemas.Administration))
            .Options;

        return new PxaDbContext(options);
    }
}
