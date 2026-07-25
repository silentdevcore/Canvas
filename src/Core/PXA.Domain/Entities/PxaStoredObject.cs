namespace PXA.Domain.Entities;

public sealed class PxaStoredObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public required string ObjectKey { get; set; }
    public required string Purpose { get; set; }
    public required string ContentType { get; set; }
    public string? FileName { get; set; }
    public long Length { get; set; }
    public required string Checksum { get; set; }
    public PxaStoredObjectStatus Status { get; set; } = PxaStoredObjectStatus.Available;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}

public enum PxaStoredObjectStatus
{
    Available,
    Deleted,
    Orphaned,
}
