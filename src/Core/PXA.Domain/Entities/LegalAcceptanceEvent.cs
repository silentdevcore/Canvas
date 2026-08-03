namespace PXA.Domain.Entities;

public sealed class LegalAcceptanceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid LegalDocumentVersionId { get; set; }
    public required string DocumentType { get; set; }
    public required string Decision { get; set; }
    public required string ContentHash { get; set; }
    public required string Locale { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
