namespace PXA.Domain.Entities;

public sealed class OrganizationMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public OrganizationMembershipStatus Status { get; set; } = OrganizationMembershipStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum OrganizationMembershipStatus
{
    Invited,
    Active,
    Suspended,
    Removed,
}
