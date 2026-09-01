using PXA.Importer.Signing;

namespace PXA.Importer.Redaction;

/// <summary>
/// What the apply stage strips beyond the covered page content. Every option defaults to on:
/// a redaction that leaves the same text in metadata or an attachment has not redacted it.
/// </summary>
public sealed record PdfRedactionSanitizationPolicy
{
    public bool RemoveDocumentMetadata { get; init; } = true;
    public bool RemoveEmbeddedFiles { get; init; } = true;
    public bool RemoveActionsAndJavaScript { get; init; } = true;
    public bool RemoveOverlappingAnnotations { get; init; } = true;
    public bool RemovePageThumbnails { get; init; } = true;

    /// <summary>
    /// Removes actions, form field values, and alternate descriptions wherever they occur,
    /// not only on the catalog. An action on an annotation or an outline entry can carry the
    /// same text the redaction is removing from the page.
    /// </summary>
    public bool RemoveDocumentWideActionsAndValues { get; init; } = true;

    /// <summary>
    /// Removes content that a viewer can reveal without the page changing: optional content
    /// groups, non-normal annotation appearance streams, and image alternates.
    /// </summary>
    public bool RemoveHiddenRepresentations { get; init; } = true;
}

public sealed record PdfRedactionApplyOptions
{
    public PdfRedactionSanitizationPolicy Sanitization { get; init; } = new();
    public PdfRedactionPreflightOptions Preflight { get; init; } = new();

    /// <summary>Recorded in the audit record. Never used to make a security decision.</summary>
    public string? Reason { get; init; }

    public string? Actor { get; init; }

    /// <summary>
    /// Key used to sign the audit record. This is a dedicated audit key, deliberately
    /// separate from any document signing key, so that compromising one cannot forge the
    /// other. When absent the record is still hashed, which detects change but does not
    /// prove origin.
    /// </summary>
    public IPdfSigningKey? AuditSigningKey { get; init; }
}

public sealed record PdfRedactionVerificationReport(
    bool Passed,
    int ResidualCoveredElementCount,
    int ResidualExtractableMatches,
    IReadOnlyList<PdfRedactionFinding> Findings);

/// <summary>
/// Evidence that a redaction happened, carrying hashes and region geometry but never the
/// removed content. <see cref="RecordSha256"/> covers the record's own canonical form, so a
/// later change to it is detectable. Deployments can additionally attach a dedicated audit
/// signature to prove origin.
/// </summary>
public sealed record PdfRedactionAuditRecord(
    string SourceSha256,
    string ResultSha256,
    DateTimeOffset AppliedAt,
    string? Actor,
    string? Reason,
    IReadOnlyList<PdfRedactionRegion> Regions,
    IReadOnlyList<string> AppliedSanitizations,
    bool VerificationPassed,
    string RecordSha256,
    PdfRedactionAuditSignature? Signature = null);

/// <summary>
/// A detached CMS signature over <see cref="PdfRedactionAuditRecord.RecordSha256"/>, and the
/// identity that produced it. The signature covers the record's canonical hash, so it proves
/// both origin and that the record has not changed.
/// </summary>
public sealed record PdfRedactionAuditSignature(
    string Algorithm,
    string SignerSubject,
    string SignerThumbprint,
    DateTimeOffset SignedAt,
    string Cms);

public sealed record PdfRedactionApplyResult(
    byte[] Bytes,
    PdfRedactionPreflightReport Preflight,
    int RemovedElementCount,
    int RemovedAnnotationCount,
    IReadOnlyList<int> RewrittenPages,
    PdfRedactionVerificationReport Verification,
    PdfRedactionAuditRecord Audit);

/// <summary>
/// Thrown when preflight reports content that cannot be confidently removed. Applying
/// anyway would return a file that looks redacted and is not.
/// </summary>
public sealed class PdfRedactionBlockedException(string message, PdfRedactionPreflightReport preflight)
    : Exception(message)
{
    public PdfRedactionPreflightReport Preflight { get; } = preflight;
}

public interface IPdfRedactionApplier
{
    Task<PdfRedactionApplyResult> ApplyAsync(
        Stream pdfStream,
        IReadOnlyList<PdfRedactionRegion> regions,
        PdfRedactionApplyOptions? options = null,
        CancellationToken cancellationToken = default);
}
