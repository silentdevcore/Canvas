using System.Text.Json;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Application.Identity;

/// <summary>
/// Owns the pending and activated Trial shapes. Entity writes remain on the
/// caller's context so registration and verification control their respective
/// transaction boundaries.
/// </summary>
public sealed class TrialActivationService(PxaDbContext dbContext)
{
    private static readonly string[] TrialCapabilities =
    [
        "generator", "designer", "migration", "importer",
        "pdf-viewer", "spreadsheet", "api", "sdk",
    ];

    public OrganizationSubscription CreatePendingTrialForNewOrganization(
        Organization organization,
        SubscriptionAccountType accountType,
        DateTimeOffset now)
    {
        var subscription = new OrganizationSubscription
        {
            OrganizationId = organization.Id,
            Edition = SubscriptionEdition.Trial,
            AccountType = accountType,
            Status = SubscriptionStatus.Pending,
            BillingPeriod = SubscriptionBillingPeriod.None,
            DeploymentMode = SubscriptionDeploymentMode.Cloud,
            SeatLimit = accountType == SubscriptionAccountType.IndividualDeveloper ? 1 : null,
            StartsAt = now,
            CurrentPeriodStartsAt = now,
        };
        dbContext.OrganizationSubscriptions.Add(subscription);
        return subscription;
    }

    public void ActivatePendingTrial(
        OrganizationSubscription subscription,
        OrganizationMembership membership,
        DateTimeOffset now)
    {
        if (subscription.Status != SubscriptionStatus.Pending)
            throw new InvalidOperationException("Only a pending subscription can be activated as a Trial.");

        subscription.Status = SubscriptionStatus.Trialing;
        subscription.StartsAt = now;
        subscription.CurrentPeriodStartsAt = now;
        subscription.TrialEndsAt = now.AddDays(30);
        subscription.UpdatedAt = now;
        dbContext.SubscriptionEntitlements.AddRange(TrialCapabilities.Select(capability =>
            new SubscriptionEntitlement
            {
                SubscriptionId = subscription.Id,
                Capability = capability,
                Enabled = true,
                Source = EntitlementSource.EditionDefault,
                ExpiresAt = subscription.TrialEndsAt,
            }));
        dbContext.SubscriptionSeatAssignments.Add(new SubscriptionSeatAssignment
        {
            SubscriptionId = subscription.Id,
            OrganizationMembershipId = membership.Id,
            AssignedByUserId = membership.UserId,
        });
        dbContext.SubscriptionLifecycleEvents.Add(new SubscriptionLifecycleEvent
        {
            SubscriptionId = subscription.Id,
            OrganizationId = subscription.OrganizationId,
            ActorUserId = membership.UserId,
            Action = "subscription.trial.started",
            CurrentStatus = SubscriptionStatus.Trialing,
            DetailsJson = JsonSerializer.Serialize(new { subscription.AccountType, TrialDays = 30 }),
        });
    }
}
