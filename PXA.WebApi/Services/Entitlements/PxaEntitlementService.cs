using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Observability;

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
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var decision = await EvaluateCoreAsync(
                organizationId,
                capability,
                quantity,
                cancellationToken);
            PxaTelemetry.RecordLicensingOperation(
                "entitlement",
                decision.Allowed ? "allowed" : "denied",
                stopwatch.Elapsed);
            return decision;
        }
        catch (OperationCanceledException)
        {
            PxaTelemetry.RecordLicensingOperation(
                "entitlement",
                "cancelled",
                stopwatch.Elapsed);
            throw;
        }
        catch
        {
            PxaTelemetry.RecordLicensingOperation(
                "entitlement",
                "failed",
                stopwatch.Elapsed);
            throw;
        }
    }

    private async Task<PxaEntitlementDecision> EvaluateCoreAsync(
        Guid organizationId,
        string capability,
        long quantity,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var organizationIsActive = await dbContext.Organizations.AsNoTracking()
            .AnyAsync(value =>
                value.Id == organizationId &&
                value.Status == OrganizationStatus.Active,
                cancellationToken);
        if (!organizationIsActive)
            return Denied("PXA_ORGANIZATION_INACTIVE", "The organization is not active.");

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
        var used = await dbContext.SubscriptionUsageEvents.AsNoTracking()
            .Where(value => value.SubscriptionId == subscription.Id &&
                            value.Capability == normalizedCapability &&
                            value.OccurredAt >= subscription.CurrentPeriodStartsAt)
            .SumAsync(value => (long?)value.Quantity, cancellationToken) ?? 0;
        if (entitlement.Limit is { } limit && used + quantity > limit)
            return Denied("PXA_ENTITLEMENT_LIMIT_EXCEEDED", "Requested quantity exceeds the remaining configured limit.",
                subscription, entitlement, used);

        return new PxaEntitlementDecision(
            true,
            "PXA_ENTITLEMENT_ALLOWED",
            "Capability is available.",
            subscription.Id,
            subscription.Edition.ToString(),
            entitlement.Capability,
            entitlement.Limit,
            entitlement.Unit,
            entitlement.ExpiresAt,
            used,
            entitlement.Limit is { } allowedLimit ? Math.Max(0, allowedLimit - used) : null);
    }

    private static PxaEntitlementDecision Denied(
        string code,
        string reason,
        OrganizationSubscription? subscription = null,
        SubscriptionEntitlement? entitlement = null,
        long used = 0) =>
        new(false, code, reason, subscription?.Id, subscription?.Edition.ToString(),
            entitlement?.Capability, entitlement?.Limit, entitlement?.Unit, entitlement?.ExpiresAt,
            used, entitlement?.Limit is { } limit ? Math.Max(0, limit - used) : null);
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
    DateTimeOffset? ExpiresAt,
    long Used,
    long? Remaining);
