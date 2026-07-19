using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Application.Subscriptions;

/// <summary>
/// Shared read-only subscription/seat/history/usage queries used by both the
/// System-Administrator-facing Admin controllers and the tenant-scoped
/// Account controllers, so the two surfaces render the same underlying data
/// without duplicating (and risking drift in) the aggregation logic. Callers
/// map the shared records to their own response DTOs - the Account DTOs
/// deliberately omit fields (raw entitlement source internals, cross-tenant
/// identifiers) that are fine for an operator to see but not a customer.
/// </summary>
public sealed class SubscriptionQueryService(PxaDbContext dbContext)
{
    public async Task<Guid?> GetSubscriptionIdForOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken) =>
        await dbContext.OrganizationSubscriptions.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SubscriptionRecord?> GetSubscriptionAsync(
        Guid subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == subscriptionId, cancellationToken);
        if (subscription is null)
            return null;

        var organizationName = await dbContext.Organizations.AsNoTracking()
            .Where(value => value.Id == subscription.OrganizationId)
            .Select(value => value.Name)
            .SingleAsync(cancellationToken);
        var entitlements = await dbContext.SubscriptionEntitlements.AsNoTracking()
            .Where(value => value.SubscriptionId == subscriptionId)
            .OrderBy(value => value.Capability)
            .Select(value => new SubscriptionEntitlementRecord(
                value.Capability, value.Enabled, value.Limit, value.Unit, value.Source.ToString(), value.ExpiresAt))
            .ToListAsync(cancellationToken);
        var assignedSeats = await dbContext.SubscriptionSeatAssignments.AsNoTracking()
            .CountAsync(value => value.SubscriptionId == subscriptionId && value.RevokedAt == null, cancellationToken);

        return new SubscriptionRecord(
            subscription.Id, subscription.OrganizationId, organizationName,
            subscription.Edition.ToString(), subscription.AccountType.ToString(), subscription.Status.ToString(),
            subscription.BillingPeriod.ToString(), subscription.DeploymentMode.ToString(),
            subscription.SeatLimit, assignedSeats, subscription.StartsAt, subscription.CurrentPeriodStartsAt,
            subscription.TrialEndsAt, subscription.CurrentPeriodEndsAt, subscription.CancellationEffectiveAt,
            subscription.GracePeriodEndsAt, entitlements, subscription.CreatedAt, subscription.UpdatedAt);
    }

    public async Task<IReadOnlyList<SubscriptionSeatRecord>> GetSeatsAsync(
        Guid subscriptionId, Guid organizationId, CancellationToken cancellationToken) =>
        await (
                from membership in dbContext.OrganizationMemberships.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
                where membership.OrganizationId == organizationId &&
                      membership.Status != OrganizationMembershipStatus.Removed
                orderby user.DisplayName
                select new SubscriptionSeatRecord(
                    membership.Id,
                    user.Id,
                    user.DisplayName,
                    user.Email ?? string.Empty,
                    membership.Status.ToString(),
                    dbContext.SubscriptionSeatAssignments.Any(assignment =>
                        assignment.SubscriptionId == subscriptionId &&
                        assignment.OrganizationMembershipId == membership.Id &&
                        assignment.RevokedAt == null)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubscriptionHistoryRecord>> GetHistoryAsync(
        Guid subscriptionId, CancellationToken cancellationToken) =>
        await (
                from lifecycleEvent in dbContext.SubscriptionLifecycleEvents.AsNoTracking()
                join actor in dbContext.Users.AsNoTracking() on lifecycleEvent.ActorUserId equals actor.Id
                where lifecycleEvent.SubscriptionId == subscriptionId
                orderby lifecycleEvent.CreatedAt descending
                select new SubscriptionHistoryRecord(
                    lifecycleEvent.Id,
                    lifecycleEvent.Action,
                    lifecycleEvent.PreviousStatus == null ? null : lifecycleEvent.PreviousStatus.ToString(),
                    lifecycleEvent.CurrentStatus.ToString(),
                    lifecycleEvent.ActorUserId,
                    actor.DisplayName,
                    lifecycleEvent.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<SubscriptionUsageRecord> GetUsageAsync(
        Guid subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .SingleAsync(value => value.Id == subscriptionId, cancellationToken);
        var aggregates = await dbContext.SubscriptionUsageEvents.AsNoTracking()
            .Where(value => value.SubscriptionId == subscriptionId &&
                            value.OccurredAt >= subscription.CurrentPeriodStartsAt)
            .GroupBy(value => new { value.Capability, value.Operation, value.Source })
            .Select(group => new
            {
                group.Key.Capability,
                group.Key.Operation,
                group.Key.Source,
                Quantity = group.Sum(value => value.Quantity),
                EventCount = group.Count(),
                LastOccurredAt = group.Max(value => value.OccurredAt),
            })
            .ToListAsync(cancellationToken);
        var items = aggregates
            .OrderBy(value => value.Capability)
            .ThenBy(value => value.Operation)
            .Select(value => new SubscriptionUsageItemRecord(
                value.Capability, value.Operation, value.Source, value.Quantity, value.EventCount, value.LastOccurredAt))
            .ToArray();
        return new SubscriptionUsageRecord(
            subscription.CurrentPeriodStartsAt, subscription.CurrentPeriodEndsAt,
            items.Sum(value => value.Quantity), items);
    }
}

public sealed record SubscriptionEntitlementRecord(
    string Capability, bool Enabled, long? Limit, string? Unit, string Source, DateTimeOffset? ExpiresAt);

public sealed record SubscriptionRecord(
    Guid Id, Guid OrganizationId, string OrganizationName, string Edition, string AccountType, string Status,
    string BillingPeriod, string DeploymentMode, int? SeatLimit, int AssignedSeats,
    DateTimeOffset StartsAt, DateTimeOffset CurrentPeriodStartsAt, DateTimeOffset? TrialEndsAt,
    DateTimeOffset? CurrentPeriodEndsAt, DateTimeOffset? CancellationEffectiveAt, DateTimeOffset? GracePeriodEndsAt,
    IReadOnlyList<SubscriptionEntitlementRecord> Entitlements, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record SubscriptionSeatRecord(
    Guid MembershipId, Guid UserId, string DisplayName, string Email, string MembershipStatus, bool Assigned);

public sealed record SubscriptionHistoryRecord(
    Guid Id, string Action, string? PreviousStatus, string CurrentStatus,
    Guid ActorUserId, string ActorName, DateTimeOffset CreatedAt);

public sealed record SubscriptionUsageItemRecord(
    string Capability, string Operation, string Source, long Quantity, int EventCount, DateTimeOffset LastOccurredAt);

public sealed record SubscriptionUsageRecord(
    DateTimeOffset PeriodStartsAt, DateTimeOffset? PeriodEndsAt, long TotalQuantity,
    IReadOnlyList<SubscriptionUsageItemRecord> Items);
