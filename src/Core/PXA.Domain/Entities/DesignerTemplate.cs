namespace PXA.Domain.Entities;

public sealed class DesignerTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? ExternalId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public DesignerTemplateStatus Status { get; set; } = DesignerTemplateStatus.Draft;
    public long Revision { get; set; } = 1;
    public required string DraftJson { get; set; }
    public required string DraftChecksum { get; set; }
    public required string SchemaVersion { get; set; }
    public required string DesignerVersion { get; set; }
    public Guid? PublishedVersionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ArchivedAt { get; set; }
}

public enum DesignerTemplateStatus
{
    Draft,
    Archived,
}
