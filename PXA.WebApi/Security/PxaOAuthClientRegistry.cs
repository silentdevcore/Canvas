using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace PXA.WebApi.Security;

public interface IPxaOAuthClientRegistry
{
    string GetServiceAccountClientId(Guid apiKeyId);
    Task RegisterServiceAccountClientAsync(
        Guid apiKeyId,
        string secret,
        string displayName,
        CancellationToken cancellationToken = default);
    Task RevokeServiceAccountClientAsync(
        Guid apiKeyId,
        CancellationToken cancellationToken = default);
}

public sealed class DisabledPxaOAuthClientRegistry : IPxaOAuthClientRegistry
{
    public string GetServiceAccountClientId(Guid apiKeyId) => PxaOAuthClients.ForApiKey(apiKeyId);

    public Task RegisterServiceAccountClientAsync(
        Guid apiKeyId,
        string secret,
        string displayName,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RevokeServiceAccountClientAsync(
        Guid apiKeyId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class PxaOAuthClientRegistry(
    IOpenIddictApplicationManager applicationManager,
    IOptions<PxaOAuthOptions> options) : IPxaOAuthClientRegistry
{
    private readonly PxaOAuthOptions settings = options.Value;

    public string GetServiceAccountClientId(Guid apiKeyId) => PxaOAuthClients.ForApiKey(apiKeyId);

    public async Task RegisterServiceAccountClientAsync(
        Guid apiKeyId,
        string secret,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return;

        var clientId = GetServiceAccountClientId(apiKeyId);
        if (await applicationManager.FindByClientIdAsync(clientId, cancellationToken) is not null)
            throw new InvalidOperationException($"OAuth client '{clientId}' already exists.");

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = displayName,
        };
        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
            .. PxaOAuthScopes.ReadOnly.Select(scope =>
                OpenIddictConstants.Permissions.Prefixes.Scope + scope),
        ]);
        await applicationManager.CreateAsync(descriptor, cancellationToken);
    }

    public async Task RevokeServiceAccountClientAsync(
        Guid apiKeyId,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return;

        var application = await applicationManager.FindByClientIdAsync(
            GetServiceAccountClientId(apiKeyId), cancellationToken);
        if (application is not null)
            await applicationManager.DeleteAsync(application, cancellationToken);
    }
}
