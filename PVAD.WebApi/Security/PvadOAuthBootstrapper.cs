using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using PVAD.Infrastructure.Persistence;

namespace PVAD.WebApi.Security;

public static class PvadOAuthBootstrapper
{
    public static async Task InitializeAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<PvadOAuthOptions>>().Value;
        if (!settings.Enabled)
            return;

        var dbContext = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        if ((await dbContext.Database.GetPendingMigrationsAsync()).Any())
        {
            if (app.Environment.IsEnvironment("Testing"))
                return;
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
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        if (settings.MigrateLegacyScopeAliases)
            await ActivatorUtilities.CreateInstance<PvadOAuthScopeMigration>(services).RunAsync(dryRun: false);
        await EnsurePublicClientAsync(manager, settings);
        if (!string.IsNullOrWhiteSpace(settings.McpServerClientSecret))
            await EnsureMcpServerClientAsync(manager, settings.McpServerClientSecret);
    }

    private static async Task EnsurePublicClientAsync(
        IOpenIddictApplicationManager manager,
        PvadOAuthOptions settings)
    {
        if (await manager.FindByClientIdAsync(settings.PublicClientId) is not null)
            return;

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = settings.PublicClientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = "PVADE MCP interactive clients",
        };
        foreach (var redirectUri in settings.PublicClientRedirectUris)
            descriptor.RedirectUris.Add(new Uri(redirectUri));
        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            .. PvadScopeCompatibility.ReadOnlyScopes.Select(scope => OpenIddictConstants.Permissions.Prefixes.Scope + scope),
        ]);
        descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        await manager.CreateAsync(descriptor);
    }

    private static async Task EnsureMcpServerClientAsync(
        IOpenIddictApplicationManager manager,
        string secret)
    {
        if (await manager.FindByClientIdAsync(PvadOAuthClients.McpServer) is not null)
            return;
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = PvadOAuthClients.McpServer,
            ClientSecret = secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = "PVADE MCP internal server",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
            },
        });
    }
}
