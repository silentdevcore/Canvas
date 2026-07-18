namespace PXA.Domain.Entities;

public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public required string IpAddressHash { get; set; }
    public required string UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public string? RevocationReason { get; set; }
}
