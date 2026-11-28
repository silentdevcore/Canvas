using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Licensing;

namespace PVAD.WebApi.Services.Licensing;

internal sealed record PvadAccountPolicySnapshot(PvadLicenseAccountKind Kind, string[] ApprovedMigrations)
{
    internal string[] AllowedCapabilities()
    {
        using var scope = PvadAccountCapabilityContext.Enter(Kind, ApprovedMigrations);
        return PvadAccountCapabilityContext.Capture()!;
    }
}

internal static class PvadAccountPolicyService
{
    internal static async Task<PvadAccountPolicySnapshot?> ReadAsync(PvadDbContext db, Guid organizationId,
        CancellationToken cancellationToken, bool requireActive = false)
    {
        var subscription = await db.OrganizationSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId, cancellationToken);
        if (subscription is null) return null;
        var now = DateTimeOffset.UtcNow;
        if (requireActive && (subscription.Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.GracePeriod) ||
            subscription.CancellationEffectiveAt <= now ||
            subscription.Status == SubscriptionStatus.Trialing && (subscription.TrialEndsAt is null || subscription.TrialEndsAt <= now) ||
            subscription.Status == SubscriptionStatus.GracePeriod && (subscription.GracePeriodEndsAt is null || subscription.GracePeriodEndsAt <= now))) return null;
        var kind = subscription.AccountType switch
        {
            SubscriptionAccountType.Company => PvadLicenseAccountKind.Company,
            SubscriptionAccountType.IndividualDeveloper => PvadLicenseAccountKind.IndividualDeveloper,
            _ => (PvadLicenseAccountKind)(-1),
        };
        // Negotiated grants are maintained by the PVAD system-admin subscription
        // workflow. Trial/edition defaults and role permissions are not approval.
        var approvals = await db.SubscriptionEntitlements.AsNoTracking()
            .Where(value => value.SubscriptionId == subscription.Id && value.Enabled &&
                value.Source != EntitlementSource.EditionDefault &&
                (value.ExpiresAt == null || value.ExpiresAt > now) &&
                (value.Capability == "migration" || value.Capability.StartsWith("migration.")))
            .Select(value => value.Capability).ToArrayAsync(cancellationToken);
        var products = PvadLicenseProducts.All.Where(product => product.StartsWith("migration.", StringComparison.Ordinal));
        return new(kind, products.Where(product => approvals.Contains("migration") || approvals.Contains(product)).ToArray());
    }
}
