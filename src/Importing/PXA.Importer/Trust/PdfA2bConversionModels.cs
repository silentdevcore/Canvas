namespace PXA.Importer.Trust;

/// <summary>Archival profile targeted by a conversion.</summary>
public enum PdfArchiveProfile
{
    /// <summary>PDF/A-2b. Embedded files are not permitted.</summary>
    PdfA2b,

    /// <summary>PDF/A-3b. Arbitrary embedded source files and attachments are permitted.</summary>
    PdfA3b
}

public enum PdfComplianceConversionCapability
{
    Ready,
    ReviewRequired,
    Unsupported
}

public sealed record PdfComplianceConversionFinding(
    string Code,
    PdfTrustDiagnosticSeverity Severity,
    string Message,
    int? ObjectNumber = null,
    int? Generation = null);

public sealed record PdfA2bConversionPreflightReport(
    string TargetProfile,
    PdfComplianceConversionCapability Capability,
    string SourceSha256,
    long FileSizeBytes,
    int ObjectCount,
    int SignatureCount,
    bool InvalidatesSignatures,
    bool OriginalPreserved,
    IReadOnlyList<PdfComplianceConversionFinding> Findings);

public sealed record PdfA2bConversionOptions
{
    public const long DefaultMaximumFileSizeBytes = 100L * 1024 * 1024;

    public long MaximumFileSizeBytes { get; init; } = DefaultMaximumFileSizeBytes;
    public bool AllowSignatureInvalidation { get; init; }

    /// <summary>Target profile. PDF/A-3b additionally permits embedded files.</summary>
    public PdfArchiveProfile Profile { get; init; } = PdfArchiveProfile.PdfA2b;
}

public sealed record PdfA2bConversionResult(
    byte[] Bytes,
    string SourceSha256,
    string ResultSha256,
    PdfA2bConversionPreflightReport Preflight,
    PdfComplianceClaim Validation);

public sealed class PdfA2bConversionBlockedException(
    string message,
    PdfA2bConversionPreflightReport preflight) : InvalidOperationException(message)
{
    public PdfA2bConversionPreflightReport Preflight { get; } = preflight;
}

public interface IPdfA2bConverter
{
    Task<PdfA2bConversionPreflightReport> PreflightAsync(
        Stream pdfStream,
        PdfA2bConversionOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<PdfA2bConversionResult> ConvertAsync(
        Stream pdfStream,
        PdfA2bConversionOptions? options = null,
        CancellationToken cancellationToken = default);
}
