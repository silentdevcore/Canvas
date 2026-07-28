using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string DataSourceName = "pxa-database";

    public static IServiceCollection AddPxaPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
        {
            Name = DataSourceName,
        };
        services.AddSingleton(dataSourceBuilder.Build());
        services.AddDbContext<PxaDbContext>((serviceProvider, options) =>
            options.UseNpgsql(
                serviceProvider.GetRequiredService<NpgsqlDataSource>(),
                postgres => postgres.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    DatabaseSchemas.Administration)));

        services.AddIdentityCore<PxaIdentityUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<PxaIdentityRole>()
            .AddEntityFrameworkStores<PxaDbContext>()
            .AddPasswordValidator<PxaBreachedPasswordValidator>();

        return services;
    }
}
