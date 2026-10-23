using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PVAD.Infrastructure.Persistence.Identity;

namespace PVAD.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string DataSourceName = "pxa-database";

    public static IServiceCollection AddPvadPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddPvadPersistence(_ => connectionString);
    }

    public static IServiceCollection AddPvadPersistence(
        this IServiceCollection services,
        Func<IServiceProvider, string> resolveConnectionString)
    {
        ArgumentNullException.ThrowIfNull(resolveConnectionString);

        services.AddSingleton(serviceProvider =>
        {
            var connectionString = resolveConnectionString(serviceProvider);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            return new NpgsqlDataSourceBuilder(connectionString)
            {
                Name = DataSourceName,
            }.Build();
        });
        services.AddDbContext<PvadDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                serviceProvider.GetRequiredService<NpgsqlDataSource>(),
                postgres => postgres.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    DatabaseSchemas.Administration));
            options.UseOpenIddict();
        });

        services.AddIdentityCore<PvadIdentityUser>(options =>
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
            .AddRoles<PvadIdentityRole>()
            .AddEntityFrameworkStores<PvadDbContext>()
            .AddPasswordValidator<PvadBreachedPasswordValidator>();

        return services;
    }
}
