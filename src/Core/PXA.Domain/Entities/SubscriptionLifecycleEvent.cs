namespace PXA.Domain.Entities;

public sealed class SubscriptionLifecycleEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ActorUserId { get; set; }
    public required string Action { get; set; }
    public SubscriptionStatus? PreviousStatus { get; set; }
    public SubscriptionStatus CurrentStatus { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
