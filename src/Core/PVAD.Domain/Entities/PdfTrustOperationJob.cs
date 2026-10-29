namespace PVAD.Domain.Entities;

public sealed class PdfTrustOperationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid AuditEventId { get; set; }
    public Guid? BackgroundJobId { get; set; }
    public required string Operation { get; set; }
    public required string Policy { get; set; }
    public required string InputSha256 { get; set; }
    public string? OutputSha256 { get; set; }
    public required string Outcome { get; set; }
    public required string DiagnosticCodesJson { get; set; }
    public string? KeyIdentifier { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}
