namespace PXA.Domain.Entities;

public sealed class SubscriptionUsageEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SubscriptionId { get; set; }
    public required string Capability { get; set; }
    public required string Operation { get; set; }
    public long Quantity { get; set; }
    public required string RequestId { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
