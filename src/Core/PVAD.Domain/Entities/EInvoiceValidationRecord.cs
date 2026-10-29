namespace PVAD.Domain.Entities;

/// <summary>
/// An append-only record of one electronic-invoice validation. It exists so a tenant can later
/// prove what PVAD reported about a specific document, with which validator artifact set, and
/// when - without re-running a validator whose artifacts may since have been updated.
/// </summary>
public sealed class EInvoiceValidationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Guid ActorUserId { get; set; }

    public Guid AuditEventId { get; set; }

    /// <summary>The operation that produced the report, for example "validate" or "attach".</summary>
    public required string Operation { get; set; }

    /// <summary>SHA-256 of the exact invoice XML bytes that were validated.</summary>
    public required string DocumentSha256 { get; set; }

    /// <summary>SHA-256 of the PDF the XML was read from or written into, when there was one.</summary>
    public string? ContainerSha256 { get; set; }

    public required string Standard { get; set; }

    public required string StandardVersion { get; set; }

    public required string Profile { get; set; }

    public string? Syntax { get; set; }

    public string? GuidelineIdentifier { get; set; }

    public bool IsStructurallyValid { get; set; }

    public bool IsConformanceValidated { get; set; }

    /// <summary>"validated", "invalid" or "notEvaluated" - never inferred from the structural result.</summary>
    public required string ConformanceStatus { get; set; }

    /// <summary>
    /// Identifies the pinned official artifact set that produced the conformance verdict. Null
    /// means no artifact set was configured, which is why the verdict is "notEvaluated".
    /// </summary>
    public string? ValidatorArtifactSetId { get; set; }

    public string? ValidatorArtifactSetSha256 { get; set; }

    /// <summary>The complete diagnostic list as reported, stored verbatim.</summary>
    public required string DiagnosticsJson { get; set; }

    /// <summary>
    /// Set when this record is a revalidation of the same document under a newer artifact set.
    /// The superseded record is never modified, so both verdicts stay provable.
    /// </summary>
    public Guid? RevalidatesRecordId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When bounded retention may remove the record. Nothing modifies it before then, and the
    /// row can only ever be deleted in whole through the retention path.
    /// </summary>
    public DateTimeOffset RetainUntil { get; set; }
}
