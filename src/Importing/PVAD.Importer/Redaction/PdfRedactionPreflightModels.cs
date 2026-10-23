using PVAD.Importer.Graphics;

namespace PVAD.Importer.Redaction;

/// <summary>
/// What PVAD can promise for a region or a document. Unknown is never folded into
/// <see cref="Removable"/>.
/// </summary>
public enum PdfRedactionCapability
{
    Removable,
    ReviewRequired,
    Unsupported
}

public enum PdfRedactionCoverage
{
    None,
    Partial,
    Contained
}

/// <summary>A region to analyse, in PDF user-space coordinates for the given page.</summary>
public sealed record PdfRedactionRegion(int PageNumber, PdfRectangle Bounds);

public sealed record PdfRedactionFinding(
    string Code,
    PdfRedactionCapability Capability,
    string Message,
    int? PageNumber = null,
    int? RegionIndex = null);

/// <summary>
/// Evidence for one kind of covered content. Counts and measurements only: the covered
/// text itself never enters the report.
/// </summary>
public sealed record PdfRedactionCoveredContent(
    string Kind,
    PdfRedactionCoverage Coverage,
    PdfRedactionCapability Capability,
    int ElementCount,
    int CharacterCount = 0);

public sealed record PdfRedactionRegionReport(
    int RegionIndex,
    int PageNumber,
    PdfRectangle Bounds,
    PdfRedactionCapability Capability,
    IReadOnlyList<PdfRedactionCoveredContent> Covered,
    IReadOnlyList<PdfRedactionFinding> Findings);

public sealed record PdfRedactionPreflightReport(
    PdfRedactionCapability Capability,
    string Sha256,
    long FileSizeBytes,
    int PageCount,
    int RevisionCount,
    IReadOnlyList<PdfRedactionRegionReport> Regions,
    IReadOnlyList<PdfRedactionFinding> DocumentFindings);

public sealed record PdfRedactionPreflightOptions
{
    public const long DefaultMaximumFileSizeBytes = 100L * 1024 * 1024;
    public const int DefaultMaximumRegionCount = 1000;

    public long MaximumFileSizeBytes { get; init; } = DefaultMaximumFileSizeBytes;
    public int MaximumRegionCount { get; init; } = DefaultMaximumRegionCount;
}

/// <summary>
/// Reports what an applied redaction would remove and what PVAD cannot account for.
/// Implementations never mutate the input.
/// </summary>
public interface IPdfRedactionPreflight
{
    Task<PdfRedactionPreflightReport> PreflightAsync(
        Stream pdfStream,
        IReadOnlyList<PdfRedactionRegion> regions,
        PdfRedactionPreflightOptions? options = null,
        CancellationToken cancellationToken = default);
}
