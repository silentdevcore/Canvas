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
}

public sealed record PdfRedactionApplyOptions
{
    public PdfRedactionSanitizationPolicy Sanitization { get; init; } = new();
    public PdfRedactionPreflightOptions Preflight { get; init; } = new();

    /// <summary>Recorded in the audit record. Never used to make a security decision.</summary>
    public string? Reason { get; init; }

    public string? Actor { get; init; }
}

public sealed record PdfRedactionVerificationReport(
    bool Passed,
    int ResidualCoveredElementCount,
    int ResidualExtractableMatches,
    IReadOnlyList<PdfRedactionFinding> Findings);

/// <summary>
/// Evidence that a redaction happened, carrying hashes and region geometry but never the
/// removed content. <see cref="RecordSha256"/> covers the record's own canonical form, so a
/// later change to it is detectable; signing the record is a separate, deferred capability.
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
    string RecordSha256);

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
