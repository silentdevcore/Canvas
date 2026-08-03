namespace PXA.Domain.Entities;

public sealed class LegalPublicationApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LegalDocumentVersionId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public LegalApprovalDecision Decision { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public enum LegalApprovalDecision
{
    Approved,
    Rejected,
}
