using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PVAD.Infrastructure.Persistence.Configuration;

namespace PVAD.Infrastructure.Persistence;

public sealed class PvadDbContextFactory : IDesignTimeDbContextFactory<PvadDbContext>
{
    private const string DefaultDevelopmentConnection =
        "Host=localhost;Port=5432;Database=pvad;Username=pvad;Password=pvad-local";

    public PvadDbContext CreateDbContext(string[] args)
    {
        var connectionString = PvadEnvironment.Read("PVAD_DATABASE_CONNECTION")
            ?? DefaultDevelopmentConnection;

        var options = new DbContextOptionsBuilder<PvadDbContext>()
            .UseNpgsql(connectionString, postgres =>
                postgres.MigrationsHistoryTable("__ef_migrations_history", DatabaseSchemas.Administration))
            .Options;

        return new PvadDbContext(options);
    }
}
