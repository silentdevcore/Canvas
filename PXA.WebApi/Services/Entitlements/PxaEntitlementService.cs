using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Services.Entitlements;

public interface IPxaEntitlementService
{
    Task<PxaEntitlementDecision> EvaluateAsync(
        Guid organizationId,
        string capability,
        long quantity = 0,
        CancellationToken cancellationToken = default);
}

public sealed class PxaEntitlementService : IPxaEntitlementService
{
    private readonly PxaDbContext dbContext;

    public PxaEntitlementService(PxaDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<PxaEntitlementDecision> EvaluateAsync(
        Guid organizationId,
        string capability,
        long quantity = 0,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var subscription = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OrganizationId == organizationId, cancellationToken);
        if (subscription is null)
            return Denied("PXA_SUBSCRIPTION_MISSING", "No subscription is assigned to this organization.");
        if (subscription.CancellationEffectiveAt is { } cancellationAt && cancellationAt <= now)
            return Denied("PXA_SUBSCRIPTION_CANCELLED", "The subscription cancellation is effective.", subscription);
        if (subscription.Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.GracePeriod))
            return Denied("PXA_SUBSCRIPTION_INACTIVE", $"Subscription state {subscription.Status} does not allow product use.", subscription);
        if (subscription.Status == SubscriptionStatus.Trialing && subscription.TrialEndsAt is { } trialEnd && trialEnd <= now)
            return Denied("PXA_TRIAL_EXPIRED", "The Trial period has expired.", subscription);
        if (subscription.Status == SubscriptionStatus.GracePeriod && subscription.GracePeriodEndsAt is { } graceEnd && graceEnd <= now)
            return Denied("PXA_GRACE_PERIOD_EXPIRED", "The subscription grace period has expired.", subscription);

        var normalizedCapability = capability.Trim().ToLowerInvariant();
        var entitlement = await dbContext.SubscriptionEntitlements.AsNoTracking()
            .SingleOrDefaultAsync(value => value.SubscriptionId == subscription.Id &&
                                           value.Capability == normalizedCapability,
                cancellationToken);
        if (entitlement is null)
            return Denied("PXA_ENTITLEMENT_MISSING", "The capability is not part of this subscription.", subscription);
        if (!entitlement.Enabled)
            return Denied("PXA_ENTITLEMENT_DENIED", "The capability is disabled.", subscription, entitlement);
        if (entitlement.ExpiresAt is { } expiresAt && expiresAt <= now)
            return Denied("PXA_ENTITLEMENT_EXPIRED", "The capability grant has expired.", subscription, entitlement);
        if (quantity < 0)
            return Denied("PXA_ENTITLEMENT_QUANTITY_INVALID", "Requested quantity cannot be negative.", subscription, entitlement);
        if (entitlement.Limit is { } limit && quantity > limit)
            return Denied("PXA_ENTITLEMENT_LIMIT_EXCEEDED", "Requested quantity exceeds the configured limit.", subscription, entitlement);

        return new PxaEntitlementDecision(
            true,
            "PXA_ENTITLEMENT_ALLOWED",
            "Capability is available.",
            subscription.Id,
            subscription.Edition.ToString(),
            entitlement.Capability,
            entitlement.Limit,
            entitlement.Unit,
            entitlement.ExpiresAt);
    }

    private static PxaEntitlementDecision Denied(
        string code,
        string reason,
        OrganizationSubscription? subscription = null,
        SubscriptionEntitlement? entitlement = null) =>
        new(false, code, reason, subscription?.Id, subscription?.Edition.ToString(),
            entitlement?.Capability, entitlement?.Limit, entitlement?.Unit, entitlement?.ExpiresAt);
}

public sealed record PxaEntitlementDecision(
    bool Allowed,
    string Code,
    string Reason,
    Guid? SubscriptionId,
    string? Edition,
    string? Capability,
    long? Limit,
    string? Unit,
    DateTimeOffset? ExpiresAt);
