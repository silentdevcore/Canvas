using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPxaPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<PxaDbContext>(options =>
            options.UseNpgsql(connectionString, postgres =>
                postgres.MigrationsHistoryTable("__ef_migrations_history", DatabaseSchemas.Administration)));

        services.AddIdentityCore<PxaIdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<PxaIdentityRole>()
            .AddEntityFrameworkStores<PxaDbContext>();

        return services;
    }
}
