using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace PVAD.WebApi.Security;

public sealed record PvadOAuthClientProvisioningResult(int ClientsToCreate, bool DryRun);

// Creates the configured first-party clients when they are missing; existing clients are left unchanged.
public sealed class PvadOAuthClientProvisioner(
    IOpenIddictApplicationManager manager,
    IOptions<PvadOAuthOptions> options)
{
    private readonly PvadOAuthOptions settings = options.Value;

    public async Task<PvadOAuthClientProvisioningResult> RunAsync(
        bool dryRun = false, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled) return new(0, dryRun);
        var pending = new List<OpenIddictApplicationDescriptor>();
        foreach (var descriptor in ConfiguredClients())
            if (await manager.FindByClientIdAsync(descriptor.ClientId!, cancellationToken) is null)
                pending.Add(descriptor);

        if (!dryRun)
            foreach (var descriptor in pending)
                await manager.CreateAsync(descriptor, cancellationToken);
        return new(pending.Count, dryRun);
    }

    private IEnumerable<OpenIddictApplicationDescriptor> ConfiguredClients()
    {
        var interactive = new OpenIddictApplicationDescriptor
        {
            ClientId = settings.PublicClientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = "PVADE MCP interactive clients",
        };
        foreach (var redirectUri in settings.PublicClientRedirectUris)
            interactive.RedirectUris.Add(new Uri(redirectUri));
        interactive.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            .. PvadOAuthScopes.ReadOnly.Select(scope => OpenIddictConstants.Permissions.Prefixes.Scope + scope),
        ]);
        interactive.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        yield return interactive;

        if (string.IsNullOrWhiteSpace(settings.McpServerClientSecret)) yield break;
        yield return new OpenIddictApplicationDescriptor
        {
            ClientId = settings.McpServerClientId,
            ClientSecret = settings.McpServerClientSecret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = "PVADE MCP internal server",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
            },
        };
    }
}
