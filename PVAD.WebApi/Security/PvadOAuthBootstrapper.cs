using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using PVAD.Infrastructure.Persistence;

namespace PVAD.WebApi.Security;

public static class PvadOAuthBootstrapper
{
    public static async Task InitializeAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<PvadOAuthOptions>>().Value;
        // Integration tests initialize their isolated stores explicitly after migration.
        if (!settings.Enabled || app.Environment.IsEnvironment("Testing"))
            return;

        var dbContext = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        if ((await dbContext.Database.GetPendingMigrationsAsync()).Any())
        {
            throw new InvalidOperationException(
                "Apply PVAD database migrations before starting the OAuth server.");
        }

        await InitializeStoreAsync(scope.ServiceProvider);
    }

    public static async Task InitializeStoreAsync(IServiceProvider services)
    {
        var settings = services.GetRequiredService<IOptions<PvadOAuthOptions>>().Value;
        if (!settings.Enabled)
            return;
        var transition = ActivatorUtilities.CreateInstance<PvadOAuthClientTransition>(services);
        await transition.InitializeAsync(dryRun: true);
        if (settings.MigrateLegacyScopeAliases)
            await ActivatorUtilities.CreateInstance<PvadOAuthScopeMigration>(services).RunAsync(dryRun: false);
        await transition.InitializeAsync();
    }
}
