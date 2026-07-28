namespace PXA.Domain.Entities;

public sealed class DesignerFeaturePreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public required string FeatureId { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
