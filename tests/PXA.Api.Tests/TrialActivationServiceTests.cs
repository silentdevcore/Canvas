using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Identity;

namespace PXA.Api.Tests;

/// <summary>
/// Isolated unit coverage for the pending-to-active Trial transition:
/// it only queues writes on the change tracker (never SaveChangesAsync), so a
/// PxaDbContext backed by the EF Core InMemory provider is enough here - no
/// Postgres/Testcontainers/WebApplicationFactory needed.
/// </summary>
public sealed class TrialActivationServiceTests
{
    private static readonly string[] ExpectedCapabilities =
        ["generator", "designer", "migration", "importer", "pdf-viewer", "spreadsheet", "api", "sdk"];

    [Fact]
    public void Company_verification_activates_an_unlimited_seat_trial_with_every_capability()
    {
        using var dbContext = CreateContext();
        var organization = new Organization { Name = "Acme Inc", Slug = "acme" };
        var membership = new OrganizationMembership { OrganizationId = organization.Id, UserId = Guid.NewGuid() };
        var now = DateTimeOffset.UtcNow;
        var service = new TrialActivationService(dbContext);

        var subscription = service.CreatePendingTrialForNewOrganization(
            organization, SubscriptionAccountType.Company, now.AddHours(-1));

        Assert.Equal(SubscriptionStatus.Pending, subscription.Status);
        Assert.Null(subscription.TrialEndsAt);
        Assert.Empty(dbContext.SubscriptionEntitlements.Local);
        Assert.Empty(dbContext.SubscriptionSeatAssignments.Local);

        service.ActivatePendingTrial(subscription, membership, now);

        Assert.Equal(SubscriptionEdition.Trial, subscription.Edition);
        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.Equal(now.AddDays(30), subscription.TrialEndsAt);
        Assert.Null(subscription.SeatLimit);

        var tracked = dbContext.OrganizationSubscriptions.Local;
        Assert.Same(subscription, Assert.Single(tracked));

        var entitlements = dbContext.SubscriptionEntitlements.Local;
        Assert.Equal(ExpectedCapabilities.Length, entitlements.Count);
        Assert.Equal(ExpectedCapabilities.OrderBy(value => value), entitlements.Select(value => value.Capability).OrderBy(value => value));
        Assert.All(entitlements, entitlement =>
        {
            Assert.True(entitlement.Enabled);
            Assert.Equal(subscription.TrialEndsAt, entitlement.ExpiresAt);
            Assert.Equal(subscription.Id, entitlement.SubscriptionId);
        });

        var seatAssignment = Assert.Single(dbContext.SubscriptionSeatAssignments.Local);
        Assert.Equal(membership.Id, seatAssignment.OrganizationMembershipId);
        Assert.Equal(membership.UserId, seatAssignment.AssignedByUserId);

        var lifecycleEvent = Assert.Single(dbContext.SubscriptionLifecycleEvents.Local);
        Assert.Equal("subscription.trial.started", lifecycleEvent.Action);
        Assert.Equal(SubscriptionStatus.Trialing, lifecycleEvent.CurrentStatus);
    }

    [Fact]
    public void Individual_developer_registration_creates_a_single_seat_pending_trial()
    {
        using var dbContext = CreateContext();
        var organization = new Organization { Name = "Solo Developer's workspace", Slug = "developer-abc" };
        var service = new TrialActivationService(dbContext);

        var subscription = service.CreatePendingTrialForNewOrganization(
            organization, SubscriptionAccountType.IndividualDeveloper, DateTimeOffset.UtcNow);

        Assert.Equal(1, subscription.SeatLimit);
        Assert.Equal(SubscriptionAccountType.IndividualDeveloper, subscription.AccountType);
        Assert.Equal(SubscriptionStatus.Pending, subscription.Status);
        Assert.Null(subscription.TrialEndsAt);
    }

    private static PxaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PxaDbContext(options);
    }
}
