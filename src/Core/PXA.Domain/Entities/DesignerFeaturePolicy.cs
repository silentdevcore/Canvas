namespace PXA.Domain.Entities;

public sealed class DesignerFeaturePolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string FeatureId { get; set; }
    public bool AlphaOptInAllowed { get; set; }
    public bool? EnabledOverride { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
