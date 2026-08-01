namespace PXA.Domain.Entities;

public sealed class LegalDocumentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LegalDocumentId { get; set; }
    public required string Version { get; set; }
    public required string Locale { get; set; }
    public LegalDocumentAudience Audience { get; set; }
    public LegalDocumentStatus Status { get; set; } = LegalDocumentStatus.Draft;
    public required string SourceMarkdown { get; set; }
    public required string RenderedHtml { get; set; }
    public required string ContentHash { get; set; }
    public string? ChangeSummary { get; set; }
    public bool RequiresAcceptance { get; set; }
    public bool IsAuthoritative { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public DateTimeOffset? EffectiveAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public Guid? PreviousVersionId { get; set; }
}
public enum LegalDocumentAudience
{
    All,
    IndividualDeveloper,
    Company,
    Consumer,
    Business,
    Cloud,
    OnPremise,
}

public enum LegalDocumentStatus
{
    Draft,
    InReview,
    Approved,
    Scheduled,
    Published,
    Retired,
}
