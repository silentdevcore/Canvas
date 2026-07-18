namespace PXA.Domain.Entities;

public sealed class OrganizationSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public SubscriptionEdition Edition { get; set; }
    public SubscriptionAccountType AccountType { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;
    public SubscriptionBillingPeriod BillingPeriod { get; set; }
    public SubscriptionDeploymentMode DeploymentMode { get; set; } = SubscriptionDeploymentMode.Cloud;
    public int? SeatLimit { get; set; }
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? TrialEndsAt { get; set; }
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    public DateTimeOffset? CancellationEffectiveAt { get; set; }
    public DateTimeOffset? GracePeriodEndsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum SubscriptionEdition { Free, Trial, Premium, Enterprise }
public enum SubscriptionAccountType { IndividualDeveloper, Company }
public enum SubscriptionStatus { Pending, Trialing, Active, PastDue, GracePeriod, Suspended, Cancelled, Expired }
public enum SubscriptionBillingPeriod { None, Monthly, Annual }
public enum SubscriptionDeploymentMode { Cloud, OnPremise, Hybrid }
