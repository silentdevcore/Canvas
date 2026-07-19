using System.Text.Json;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Application.Identity;

/// <summary>
/// Owns the entitlement/seat/lifecycle-event shape of a Trial. Queues entity
/// writes on the caller's <see cref="PxaDbContext"/> without saving or
/// committing, so <see cref="CustomerRegistrationService"/> (and any future
/// caller needing to (re)activate a Trial for an existing organization) stays
/// in control of the transaction boundary.
/// </summary>
public sealed class TrialActivationService(PxaDbContext dbContext)
{
    private static readonly string[] TrialCapabilities =
    [
        "generator", "designer", "migration", "importer",
        "pdf-viewer", "spreadsheet", "api", "sdk",
    ];

    public OrganizationSubscription ActivateTrialForNewOrganization(
        Organization organization,
        OrganizationMembership membership,
        SubscriptionAccountType accountType,
        DateTimeOffset now)
    {
        var subscription = new OrganizationSubscription
        {
            OrganizationId = organization.Id,
            Edition = SubscriptionEdition.Trial,
            AccountType = accountType,
            Status = SubscriptionStatus.Trialing,
            BillingPeriod = SubscriptionBillingPeriod.None,
            DeploymentMode = SubscriptionDeploymentMode.Cloud,
            SeatLimit = accountType == SubscriptionAccountType.IndividualDeveloper ? 1 : null,
            StartsAt = now,
            CurrentPeriodStartsAt = now,
            TrialEndsAt = now.AddDays(30),
        };
        dbContext.OrganizationSubscriptions.Add(subscription);
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
            OrganizationId = organization.Id,
            ActorUserId = membership.UserId,
            Action = "subscription.trial.started",
            CurrentStatus = SubscriptionStatus.Trialing,
            DetailsJson = JsonSerializer.Serialize(new { AccountType = accountType, TrialDays = 30 }),
        });
        return subscription;
    }
}
