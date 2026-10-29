using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed class PvadOAuthClientTransitionTests
{
    private const string Secret = "synthetic-client-transition-secret-not-a-real-credential";
    private static readonly Guid KeyId = Guid.Parse("46294282-36bc-49fd-bd49-ae0bbf25bbbf");

    [Fact]
    public void Defaults_and_service_account_ids_are_canonical_and_legacy_parsing_is_bounded()
    {
        var options = new PvadOAuthOptions();
        Assert.Equal("pvad-api", options.ApiResource);
        Assert.Equal("pvad-mcp-public", options.PublicClientId);
        Assert.Equal("pvad-mcp-server", options.McpServerClientId);
        Assert.False(options.MigrateLegacyClientIdentities);
        foreach (var id in new[] { PvadOAuthClients.ForApiKey(KeyId), PvadOAuthClients.LegacyForApiKey(KeyId) })
        {
            Assert.True(PvadOAuthClients.TryParseServiceAccountId(id, out var parsed));
            Assert.Equal(KeyId, parsed);
        }
        foreach (var id in new string?[] { null, "", "pvad-sa-", "PVAD-sa-" + KeyId.ToString("N"),
                     "pvad-sa-" + KeyId, "pvad-sa-" + Guid.Empty.ToString("N"), "other-sa-" + KeyId.ToString("N"),
                     PvadOAuthClients.ForApiKey(KeyId) + "trailing", "pvad-mcp-dcr-" + KeyId.ToString("N"),
                     PvadOAuthClients.ForApiKey(KeyId) + " ", "pxa-sa- " + KeyId.ToString("N") })
            Assert.False(PvadOAuthClients.TryParseServiceAccountId(id, out _));
    }

    [Fact]
    public async Task Fresh_store_registers_current_clients_idempotently_without_legacy_counterparts()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        await db.Database.EnsureCreatedAsync();
        await PvadOAuthBootstrapper.InitializeStoreAsync(scope.ServiceProvider);
        await PvadOAuthBootstrapper.InitializeStoreAsync(scope.ServiceProvider);
        Assert.Equal(new[] { "pvad-mcp-public", "pvad-mcp-server" },
            await db.Set<OpenIddictEntityFrameworkCoreApplication>().OrderBy(value => value.ClientId).Select(value => value.ClientId).ToArrayAsync());
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var interactive = (await manager.FindByClientIdAsync(PvadOAuthClients.Public))!;
        var permissions = await manager.GetPermissionsAsync(interactive);
        Assert.Contains("scp:pvad.docs.read", permissions);
        Assert.Contains("scp:pxa.docs.read", permissions);
        Assert.DoesNotContain("scp:pvad.templates.write", permissions);
        Assert.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            await manager.GetRequirementsAsync(interactive));
    }

    [Fact]
    public Task Retained_clients_grants_tokens_and_credentials_survive_explicit_transition() => ExerciseAsync(null);

    [Fact]
    public async Task Mismatched_retained_client_type_fails_before_any_creation()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PvadDbContext>();
        await db.Database.EnsureCreatedAsync();
        await services.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = PvadOAuthClients.LegacyPublic,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ClientSecret = Secret,
            Permissions = { "ept:token", "gt:client_credentials" },
        });
        var transition = ActivatorUtilities.CreateInstance<PvadOAuthClientTransition>(services);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transition.RunAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => transition.RunAsync(false));
        Assert.Single(await db.Set<OpenIddictEntityFrameworkCoreApplication>().ToArrayAsync());
    }

    [Fact]
    public async Task Startup_flag_is_required_for_retained_clients_and_does_not_rewrite_them()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PvadDbContext>();
        await db.Database.EnsureCreatedAsync();
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var legacy = await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = PvadOAuthClients.LegacyPublic,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            Permissions = { "ept:authorization", "ept:token", "gt:authorization_code", "rst:code", "scp:pxa.docs.read" },
        });
        var original = Snapshot(db, legacy);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PvadOAuthBootstrapper.InitializeStoreAsync(services));
        services.GetRequiredService<IOptions<PvadOAuthOptions>>().Value.MigrateLegacyClientIdentities = true;
        await PvadOAuthBootstrapper.InitializeStoreAsync(services);
        Assert.NotNull(await manager.FindByClientIdAsync(PvadOAuthClients.Public));
        Assert.Equal(original, Snapshot(db, legacy));
    }

    [PostgreSqlFact]
    public async Task PostgreSql_retained_clients_survive_transition_and_rollback()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        await ExerciseAsync(postgres.GetConnectionString());
    }

    private static async Task ExerciseAsync(string? connectionString)
    {
        await using var provider = Services(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PvadDbContext>();
        await db.Database.EnsureCreatedAsync();
        var clients = services.GetRequiredService<IOpenIddictApplicationManager>();
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var publicClient = (OpenIddictEntityFrameworkCoreApplication)await clients.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = PvadOAuthClients.LegacyPublic,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = "Retained custom label",
            RedirectUris = { new Uri("https://retained.example.test/callback") },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange },
            Permissions = { "ept:authorization", "ept:token", "gt:authorization_code", "rst:code", "scp:pxa.docs.read", "scp:pxa.unknown" },
        });
        var confidential = (OpenIddictEntityFrameworkCoreApplication)await clients.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = PvadOAuthClients.LegacyMcpServer,
            ClientSecret = Secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = "Retained internal client",
            Permissions = { "ept:token", "gt:client_credentials" },
        });
        var grant = (OpenIddictEntityFrameworkCoreAuthorization)await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = publicClient.Id, Subject = "synthetic-user",
            Status = OpenIddictConstants.Statuses.Valid, Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            Scopes = { "pxa.docs.read" },
        });
        var revoked = (OpenIddictEntityFrameworkCoreAuthorization)await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = publicClient.Id, Subject = "synthetic-revoked",
            Status = OpenIddictConstants.Statuses.Revoked, Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            Scopes = { "pxa.docs.read" },
        });
        var token = new OpenIddictEntityFrameworkCoreToken
        {
            Application = publicClient, Authorization = grant, Subject = "synthetic-user",
            Status = OpenIddictConstants.Statuses.Valid, Type = OpenIddictConstants.TokenTypeHints.AccessToken,
            Payload = "synthetic-issued-token-bytes-retained", ExpirationDate = DateTime.UtcNow.AddMinutes(10),
        };
        db.Add(token);
        await db.SaveChangesAsync();
        var oldPublic = Snapshot(db, publicClient);
        var oldConfidential = Snapshot(db, confidential);
        var oldGrant = Snapshot(db, grant);
        var oldRevoked = Snapshot(db, revoked);
        var oldToken = Snapshot(db, token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => PvadOAuthBootstrapper.InitializeStoreAsync(services));
        Assert.Equal(2, await db.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync());
        var transition = ActivatorUtilities.CreateInstance<PvadOAuthClientTransition>(services);
        Assert.Equal(new(2, true), await transition.RunAsync());
        Assert.Equal(2, await db.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync());

        var options = services.GetRequiredService<IOptions<PvadOAuthOptions>>().Value;
        options.McpServerClientSecret = "synthetic-mismatched-secret-not-for-output";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => transition.RunAsync(false));
        Assert.DoesNotContain(options.McpServerClientSecret, error.ToString());
        Assert.Equal(2, await db.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync());
        options.McpServerClientSecret = Secret;
        Assert.Equal(new(2, false), await transition.RunAsync(false));
        Assert.Equal(new(0, false), await transition.RunAsync(false));
        Assert.Equal(4, await db.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync());
        Assert.Equal(oldPublic, Snapshot(db, publicClient));
        Assert.Equal(oldConfidential, Snapshot(db, confidential));
        Assert.Equal(oldGrant, Snapshot(db, grant));
        Assert.Equal(oldRevoked, Snapshot(db, revoked));
        Assert.Equal(oldToken, Snapshot(db, token));
        Assert.True(await clients.ValidateClientSecretAsync(confidential, Secret));
        var currentConfidential = (await clients.FindByClientIdAsync(PvadOAuthClients.McpServer))!;
        Assert.True(await clients.ValidateClientSecretAsync(currentConfidential, Secret));
        var currentPublic = (await clients.FindByClientIdAsync(PvadOAuthClients.Public))!;
        Assert.Equal("Retained custom label", await clients.GetDisplayNameAsync(currentPublic));
        Assert.Equal(await clients.GetRedirectUrisAsync(publicClient), await clients.GetRedirectUrisAsync(currentPublic));
        Assert.Equal(await clients.GetRequirementsAsync(publicClient), await clients.GetRequirementsAsync(currentPublic));
        var permissions = await clients.GetPermissionsAsync(currentPublic);
        Assert.Contains("scp:pvad.docs.read", permissions);
        Assert.Contains("scp:pxa.unknown", permissions);
        Assert.DoesNotContain("scp:pvad.unknown", permissions);
        Assert.DoesNotContain("scp:pvad.templates.read", permissions);
        Assert.DoesNotContain("scp:pvad.templates.write", permissions);

        // Rollback selects retained IDs; no token, grant or client row needs rewriting.
        options.PublicClientId = PvadOAuthClients.LegacyPublic;
        options.McpServerClientId = PvadOAuthClients.LegacyMcpServer;
        options.ApiResource = "pxa-api";
        await PvadOAuthBootstrapper.InitializeStoreAsync(services);
        Assert.Equal(4, await db.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync());
        Assert.Equal(oldToken, Snapshot(db, token));
        Assert.Equal(oldRevoked, Snapshot(db, revoked));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Service_account_registration_preserves_retained_identity_and_credentials(bool legacy)
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PvadDbContext>();
        await db.Database.EnsureCreatedAsync();
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var registry = ActivatorUtilities.CreateInstance<PvadOAuthClientRegistry>(services);
        if (legacy)
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = PvadOAuthClients.LegacyForApiKey(KeyId), ClientSecret = Secret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                Permissions = { "ept:token", "gt:client_credentials", "scp:pxa.docs.read" },
            });
        else
            await registry.RegisterServiceAccountClientAsync(KeyId, Secret, "Synthetic service account");
        var id = legacy ? PvadOAuthClients.LegacyForApiKey(KeyId) : registry.GetServiceAccountClientId(KeyId);
        var client = (await manager.FindByClientIdAsync(id))!;
        Assert.True(await manager.ValidateClientSecretAsync(client, Secret));
        var before = Snapshot(db, client);
        await ActivatorUtilities.CreateInstance<PvadOAuthClientTransition>(services).RunAsync(false);
        Assert.Equal(before, Snapshot(db, client));
        Assert.True(await manager.ValidateClientSecretAsync(client, Secret));
    }

    [PostgreSqlFact]
    public async Task PostgreSql_revocation_removes_both_service_account_client_generations()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        await using var provider = Services(postgres.GetConnectionString());
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<PvadDbContext>().Database.EnsureCreatedAsync();
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var registry = ActivatorUtilities.CreateInstance<PvadOAuthClientRegistry>(services);
        await registry.RegisterServiceAccountClientAsync(KeyId, Secret, "Synthetic current client");
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = PvadOAuthClients.LegacyForApiKey(KeyId), ClientSecret = Secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            Permissions = { "ept:token", "gt:client_credentials", "scp:pxa.docs.read" },
        });
        await registry.RevokeServiceAccountClientAsync(KeyId);
        Assert.Null(await manager.FindByClientIdAsync(PvadOAuthClients.ForApiKey(KeyId)));
        Assert.Null(await manager.FindByClientIdAsync(PvadOAuthClients.LegacyForApiKey(KeyId)));
        await registry.RevokeServiceAccountClientAsync(KeyId);
    }

    [Fact]
    public async Task Disabled_and_cancelled_transition_do_not_create_clients()
    {
        await using var provider = Services();
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PvadDbContext>();
        await db.Database.EnsureCreatedAsync();
        var options = services.GetRequiredService<IOptions<PvadOAuthOptions>>().Value;
        var transition = ActivatorUtilities.CreateInstance<PvadOAuthClientTransition>(services);
        options.Enabled = false;
        Assert.Equal(new(0, false), await transition.RunAsync(false));
        options.Enabled = true;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transition.RunAsync(false, cancelled.Token));
        Assert.Empty(await db.Set<OpenIddictEntityFrameworkCoreApplication>().ToArrayAsync());
    }

    private static ServiceProvider Services(string? connectionString = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<PvadOAuthOptions>(options => options.McpServerClientSecret = Secret);
        var name = Guid.NewGuid().ToString();
        services.AddDbContext<PvadDbContext>(options =>
        {
            if (connectionString is null) options.UseInMemoryDatabase(name);
            else options.UseNpgsql(connectionString);
            options.UseOpenIddict();
        });
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore().UseDbContext<PvadDbContext>());
        return services.BuildServiceProvider();
    }

    private static KeyValuePair<string, object?>[] Snapshot(PvadDbContext db, object entity) =>
        db.Entry(entity).Properties.OrderBy(property => property.Metadata.Name)
            .Select(property => new KeyValuePair<string, object?>(property.Metadata.Name, property.CurrentValue)).ToArray();
}
