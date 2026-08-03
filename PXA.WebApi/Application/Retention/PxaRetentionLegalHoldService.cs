using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Application.Retention;

public sealed class PxaRetentionLegalHoldService(PxaDbContext dbContext)
{
    public async Task<PxaRetentionHoldScope> GetActiveScopeAsync(
        string category,
        CancellationToken cancellationToken)
    {
        var organizationIds = await dbContext.RetentionLegalHolds.AsNoTracking()
            .Where(value => value.Category == category && value.ReleasedAt == null)
            .Select(value => value.OrganizationId)
            .ToArrayAsync(cancellationToken);
        return new PxaRetentionHoldScope(
            organizationIds.Any(value => value is null),
            organizationIds.OfType<Guid>().ToHashSet());
    }

    public IQueryable<RetentionLegalHold> Query(bool includeReleased) =>
        includeReleased
            ? dbContext.RetentionLegalHolds.AsNoTracking()
            : dbContext.RetentionLegalHolds.AsNoTracking().Where(value => value.ReleasedAt == null);
}

public sealed record PxaRetentionHoldScope(bool Global, IReadOnlySet<Guid> OrganizationIds)
{
    public bool Holds(Guid? organizationId) =>
        Global || (organizationId is not null && OrganizationIds.Contains(organizationId.Value));
}
