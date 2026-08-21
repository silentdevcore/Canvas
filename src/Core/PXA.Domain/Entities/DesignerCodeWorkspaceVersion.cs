namespace PXA.Domain.Entities;

public sealed class DesignerCodeWorkspaceVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid TemplateId { get; set; }
    public Guid TemplateVersionId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public long WorkspaceRevision { get; set; }
    public string JsonDraft { get; set; } = "";
    public string CSharpModelDraft { get; set; } = "";
    public string CSharpPdfDraft { get; set; } = "";
    public string CSharpBase64Draft { get; set; } = "";
    public string CanonicalDesignJson { get; set; } = "{}";
    public string SourceMapJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
