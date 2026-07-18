namespace PXA.Domain.Entities;

public sealed class IdentityActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public required string Purpose { get; set; }
    public required string TokenHash { get; set; }
    public required string RecipientEmail { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
