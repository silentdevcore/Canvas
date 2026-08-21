namespace PXA.Domain.Entities;

public sealed class DesignerCodeWorkspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public string JsonDraft { get; set; } = "";
    public string CSharpModelDraft { get; set; } = "";
    public string CSharpPdfDraft { get; set; } = "";
    public string CSharpBase64Draft { get; set; } = "";
    public string CanonicalDesignJson { get; set; } = "{}";
    public string SourceMapJson { get; set; } = "[]";
    public string JsonChecksum { get; set; } = "";
    public string CSharpModelChecksum { get; set; } = "";
    public string CSharpPdfChecksum { get; set; } = "";
    public string CSharpBase64Checksum { get; set; } = "";
    public string CanonicalChecksum { get; set; } = "";
    public long BaseTemplateRevision { get; set; }
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
