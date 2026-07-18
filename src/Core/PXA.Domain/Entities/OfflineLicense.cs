namespace PXA.Domain.Entities;

public sealed class OfflineLicense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SubscriptionId { get; set; }
    public required string LicenseNumber { get; set; }
    public OfflineLicenseStatus Status { get; set; } = OfflineLicenseStatus.Active;
    public required string EnvelopeJson { get; set; }
    public required string Signature { get; set; }
    public required string KeyId { get; set; }
    public required string Algorithm { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public int InstanceLimit { get; set; } = 1;
    public Guid IssuedByUserId { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
}

public enum OfflineLicenseStatus { Active, Revoked, Replaced, Expired }
