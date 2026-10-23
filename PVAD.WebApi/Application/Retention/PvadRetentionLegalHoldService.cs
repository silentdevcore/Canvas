using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;

namespace PVAD.WebApi.Application.Retention;

public sealed class PvadRetentionLegalHoldService(PvadDbContext dbContext)
{
    public async Task<PvadRetentionHoldScope> GetActiveScopeAsync(
        string category,
        CancellationToken cancellationToken)
    {
        var organizationIds = await dbContext.RetentionLegalHolds.AsNoTracking()
            .Where(value => value.Category == category && value.ReleasedAt == null)
            .Select(value => value.OrganizationId)
            .ToArrayAsync(cancellationToken);
        return new PvadRetentionHoldScope(
            organizationIds.Any(value => value is null),
            organizationIds.OfType<Guid>().ToHashSet());
    }

    public IQueryable<RetentionLegalHold> Query(bool includeReleased) =>
        includeReleased
            ? dbContext.RetentionLegalHolds.AsNoTracking()
            : dbContext.RetentionLegalHolds.AsNoTracking().Where(value => value.ReleasedAt == null);
}

public sealed record PvadRetentionHoldScope(bool Global, IReadOnlySet<Guid> OrganizationIds)
{
    public bool Holds(Guid? organizationId) =>
        Global || (organizationId is not null && OrganizationIds.Contains(organizationId.Value));
}
