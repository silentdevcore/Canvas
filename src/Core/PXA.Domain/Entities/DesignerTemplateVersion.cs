namespace PXA.Domain.Entities;

public sealed class DesignerTemplateVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public long VersionNumber { get; set; }
    public string? Label { get; set; }
    public required string DesignJson { get; set; }
    public required string Checksum { get; set; }
    public required string SchemaVersion { get; set; }
    public required string DesignerVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
