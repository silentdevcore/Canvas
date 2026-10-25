using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace PVAD.WebApi.Security;

public sealed record PvadOAuthClientTransitionResult(int ClientsToCreate, bool DryRun);

// Maintenance operation: retain old clients/grants and provision reviewed current counterparts.
public sealed class PvadOAuthClientTransition(
    IOpenIddictApplicationManager manager,
    IOptions<PvadOAuthOptions> options)
{
    private readonly PvadOAuthOptions settings = options.Value;

    public Task<PvadOAuthClientTransitionResult> RunAsync(
        bool dryRun = true, CancellationToken cancellationToken = default) =>
        ExecuteAsync(dryRun, allowLegacyTransition: true, cancellationToken);

    internal Task<PvadOAuthClientTransitionResult> InitializeAsync(
        bool dryRun = false, CancellationToken cancellationToken = default) =>
        ExecuteAsync(dryRun, settings.MigrateLegacyClientIdentities, cancellationToken);

    private async Task<PvadOAuthClientTransitionResult> ExecuteAsync(
        bool dryRun, bool allowLegacyTransition, CancellationToken cancellationToken)
    {
        if (!settings.Enabled) return new(0, dryRun);
        var pending = new List<OpenIddictApplicationDescriptor>();
        foreach (var (descriptor, legacyId) in ConfiguredClients())
        {
            if (await manager.FindByClientIdAsync(descriptor.ClientId!, cancellationToken) is not null) continue;
            var legacy = legacyId is null ? null : await manager.FindByClientIdAsync(legacyId, cancellationToken);
            if (legacy is not null)
            {
                if (!allowLegacyTransition)
                    throw new InvalidOperationException(
                        "Retained OAuth clients require a reviewed client transition. Run its dry-run and explicitly enable OAuth:MigrateLegacyClientIdentities during maintenance.");
                if (await manager.GetClientTypeAsync(legacy, cancellationToken) != descriptor.ClientType)
                    throw new InvalidOperationException("The retained OAuth client type does not match the planned counterpart.");
                if (descriptor.ClientType == OpenIddictConstants.ClientTypes.Confidential &&
                    !await manager.ValidateClientSecretAsync(legacy, descriptor.ClientSecret!, cancellationToken))
                    throw new InvalidOperationException("The configured OAuth secret does not match the retained client. Resolve credentials before client transition.");
                var retained = new OpenIddictApplicationDescriptor();
                await manager.PopulateAsync(retained, legacy, cancellationToken);
                retained.ClientId = descriptor.ClientId;
                // Populate may expose a stored hash: never pass it through plaintext hashing.
                retained.ClientSecret = descriptor.ClientSecret;
                retained.Permissions.UnionWith(PvadOAuthScopeMigration.AddAliases(
                    retained.Permissions.ToImmutableArray(), OpenIddictConstants.Permissions.Prefixes.Scope));
                pending.Add(retained);
                continue;
            }
            pending.Add(descriptor);
        }

        // Preflight retained identities before creating any client; retries are idempotent.
        if (!dryRun)
            foreach (var descriptor in pending)
                await manager.CreateAsync(descriptor, cancellationToken);
        return new(pending.Count, dryRun);
    }

    private IEnumerable<(OpenIddictApplicationDescriptor Descriptor, string? LegacyId)> ConfiguredClients()
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
            .. PvadScopeCompatibility.ReadOnlyScopes.Select(scope => OpenIddictConstants.Permissions.Prefixes.Scope + scope),
        ]);
        interactive.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        yield return (interactive, settings.PublicClientId == PvadOAuthClients.Public ? PvadOAuthClients.LegacyPublic : null);

        if (string.IsNullOrWhiteSpace(settings.McpServerClientSecret)) yield break;
        yield return (new OpenIddictApplicationDescriptor
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
        }, settings.McpServerClientId == PvadOAuthClients.McpServer ? PvadOAuthClients.LegacyMcpServer : null);
    }
}
