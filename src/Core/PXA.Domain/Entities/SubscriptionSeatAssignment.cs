namespace PXA.Domain.Entities;

public sealed class SubscriptionSeatAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public Guid OrganizationMembershipId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}
