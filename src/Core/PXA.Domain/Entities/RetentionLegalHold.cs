namespace PXA.Domain.Entities;

public sealed class RetentionLegalHold
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Category { get; set; }
    public Guid? OrganizationId { get; set; }
    public required string Reason { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ReleasedByUserId { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }

    public bool IsActive => ReleasedAt is null;
}
