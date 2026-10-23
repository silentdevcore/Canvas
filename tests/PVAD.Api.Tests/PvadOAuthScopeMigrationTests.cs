using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed class PvadOAuthScopeMigrationTests
{
    [Fact]
    public void Alias_planning_is_additive_known_only_and_idempotent()
    {
        var input = ImmutableArray.Create("scp:pxa.docs.read", "scp:pxa.unknown", "ept:token");
        var result = PvadOAuthScopeMigration.AddAliases(input, "scp:");
        Assert.Equal(4, result.Length);
        Assert.Contains("scp:pvad.docs.read", result);
        Assert.DoesNotContain("scp:pvad.unknown", result);
        Assert.DoesNotContain("scp:pvad.templates.write", result);
        Assert.Equal(result.ToArray(), PvadOAuthScopeMigration.AddAliases(result, "scp:").ToArray());
    }

    [Fact]
    public Task In_memory_store_preserves_credentials_and_grants() => ExerciseAsync(null);

    [PostgreSqlFact]
    public async Task PostgreSql_store_preserves_credentials_and_grants()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        await ExerciseAsync(postgres.GetConnectionString());
    }

    private static async Task ExerciseAsync(string? connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PvadDbContext>(options =>
        {
            if (connectionString is null) options.UseInMemoryDatabase(Guid.NewGuid().ToString());
            else options.UseNpgsql(connectionString);
            options.UseOpenIddict();
        });
        services.AddOpenIddict().AddCore(options =>
            options.UseEntityFrameworkCore().UseDbContext<PvadDbContext>());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        await db.Database.EnsureCreatedAsync();
        var clients = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var grants = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        const string secret = "synthetic-migration-secret-not-a-real-credential";
        var client = (OpenIddictEntityFrameworkCoreApplication)await clients.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "pxa-synthetic-retained-client",
            ClientSecret = secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = "Synthetic retained client",
            RedirectUris = { new Uri("https://synthetic.example.test/callback") },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange },
            Permissions = { "ept:token", "gt:client_credentials", "scp:pxa.docs.read", "scp:pxa.unknown" },
        });
        var grant = (OpenIddictEntityFrameworkCoreAuthorization)await grants.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = client.Id, Subject = "synthetic-subject",
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            Scopes = { "pxa.docs.read", "pxa.unknown" },
        });
        var revoked = (OpenIddictEntityFrameworkCoreAuthorization)await grants.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = client.Id, Subject = "synthetic-revoked",
            Status = OpenIddictConstants.Statuses.Revoked,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            Scopes = { "pxa.docs.read" },
        });
        var token = new OpenIddictEntityFrameworkCoreToken
        {
            Application = client, Authorization = grant,
            Subject = "synthetic-subject", Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeHints.AccessToken, Payload = "synthetic-signed-bytes-unchanged",
            ExpirationDate = DateTime.UtcNow.AddMinutes(5),
        };
        db.Add(token);
        await db.SaveChangesAsync();
        var clientBefore = Snapshot(db, client, "Permissions", "ConcurrencyToken");
        var grantBefore = Snapshot(db, grant, "Scopes", "ConcurrencyToken");
        var revokedBefore = Snapshot(db, revoked);
        var tokenBefore = Snapshot(db, token);
        var migration = ActivatorUtilities.CreateInstance<PvadOAuthScopeMigration>(scope.ServiceProvider);
        Assert.Equal(new(1, 1, true), await migration.RunAsync());
        Assert.DoesNotContain("pvad.docs.read", client.Permissions ?? "");
        Assert.Equal(new(1, 1, false), await migration.RunAsync(false));
        Assert.Equal(new(0, 0, false), await migration.RunAsync(false));
        db.ChangeTracker.Clear();
        var persistedClient = await db.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync();
        var persistedGrant = await db.Set<OpenIddictEntityFrameworkCoreAuthorization>().SingleAsync(value => value.Id == grant.Id);
        var persistedRevoked = await db.Set<OpenIddictEntityFrameworkCoreAuthorization>().SingleAsync(value => value.Id == revoked.Id);
        var persistedToken = await db.Set<OpenIddictEntityFrameworkCoreToken>().SingleAsync();
        Assert.Equal(clientBefore, Snapshot(db, persistedClient, "Permissions", "ConcurrencyToken"));
        Assert.Equal(grantBefore, Snapshot(db, persistedGrant, "Scopes", "ConcurrencyToken"));
        Assert.Equal(revokedBefore, Snapshot(db, persistedRevoked));
        Assert.Equal(tokenBefore, Snapshot(db, persistedToken));
        Assert.True(await clients.ValidateClientSecretAsync(persistedClient, secret));
        Assert.Contains("scp:pvad.docs.read", await clients.GetPermissionsAsync(persistedClient));
        Assert.DoesNotContain("scp:pvad.templates.write", await clients.GetPermissionsAsync(persistedClient));
        Assert.Contains("pvad.docs.read", await grants.GetScopesAsync(persistedGrant));
        Assert.DoesNotContain("pvad.unknown", await grants.GetScopesAsync(persistedGrant));
    }

    private static KeyValuePair<string, object?>[] Snapshot(PvadDbContext db, object entity, params string[] excluded) =>
        db.Entry(entity).Properties.Where(property => !excluded.Contains(property.Metadata.Name))
            .OrderBy(property => property.Metadata.Name)
            .Select(property => new KeyValuePair<string, object?>(property.Metadata.Name, property.CurrentValue)).ToArray();
}
