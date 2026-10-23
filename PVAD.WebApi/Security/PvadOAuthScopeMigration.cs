using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using PVAD.Infrastructure.Persistence;

namespace PVAD.WebApi.Security;

public sealed record PvadOAuthScopeMigrationResult(int Clients, int Authorizations, bool DryRun);

// Run during maintenance with OAuth traffic stopped: other replicas may cache clients/grants.
public sealed class PvadOAuthScopeMigration(
    PvadDbContext db,
    IOpenIddictApplicationStore<OpenIddictEntityFrameworkCoreApplication> applications,
    IOpenIddictAuthorizationStore<OpenIddictEntityFrameworkCoreAuthorization> authorizations,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager)
{
    public async Task<PvadOAuthScopeMigrationResult> RunAsync(
        bool dryRun = true, CancellationToken cancellationToken = default)
    {
        var clients = 0;
        var grants = 0;
        const int batchSize = 100;
        for (var offset = 0; ; offset += batchSize)
        {
            var batch = await db.Set<OpenIddictEntityFrameworkCoreApplication>()
                .OrderBy(value => value.Id).Skip(offset).Take(batchSize).ToListAsync(cancellationToken);
            foreach (var client in batch)
            {
                var previous = await applications.GetPermissionsAsync(client, cancellationToken);
                var next = AddAliases(previous, OpenIddictConstants.Permissions.Prefixes.Scope);
                if (previous.Length == next.Length) continue;
                clients++;
                if (dryRun) continue;
                await applications.SetPermissionsAsync(client, next, cancellationToken);
                await applicationManager.UpdateAsync(client, cancellationToken);
            }
            if (batch.Count < batchSize) break;
        }

        for (var offset = 0; ; offset += batchSize)
        {
            var batch = await db.Set<OpenIddictEntityFrameworkCoreAuthorization>()
                .Where(value => value.Status == OpenIddictConstants.Statuses.Valid)
                .OrderBy(value => value.Id).Skip(offset).Take(batchSize).ToListAsync(cancellationToken);
            foreach (var grant in batch)
            {
                var previous = await authorizations.GetScopesAsync(grant, cancellationToken);
                var next = AddAliases(previous, "");
                if (previous.Length == next.Length) continue;
                grants++;
                if (dryRun) continue;
                await authorizations.SetScopesAsync(grant, next, cancellationToken);
                await authorizationManager.UpdateAsync(grant, cancellationToken);
            }
            if (batch.Count < batchSize) break;
        }
        return new(clients, grants, dryRun);
    }

    public static ImmutableArray<string> AddAliases(ImmutableArray<string> values, string prefix)
    {
        var result = values.ToBuilder();
        foreach (var scope in PvadOAuthScopes.All)
        {
            var legacy = prefix + scope;
            var canonical = prefix + PvadScopeCompatibility.Canonical(scope);
            if (values.Contains(legacy, StringComparer.Ordinal) && !result.Contains(canonical))
                result.Add(canonical);
        }
        return result.ToImmutable();
    }
}
