namespace PXA.Domain.Entities;

public sealed class OrganizationMembershipRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationMembershipId { get; set; }
    public Guid RoleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? AssignedByUserId { get; set; }
}
