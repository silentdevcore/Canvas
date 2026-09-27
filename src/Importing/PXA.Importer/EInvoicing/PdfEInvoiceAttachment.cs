using PXA.Importer.Trust;
using PXA.Pdf;

namespace PXA.Importer.EInvoicing;

/// <summary>How an invoice attachment is written into an existing PDF.</summary>
public enum PdfEInvoiceAttachmentStrategy
{
    /// <summary>
    /// The original bytes are kept verbatim and a new revision is appended. Visible pages
    /// cannot change because every original object survives unmodified.
    /// </summary>
    IncrementalUpdate,

    /// <summary>
    /// The source is converted to PDF/A-3b first because its current archival identification
    /// forbids embedded files. This rewrites the file and is only performed after approval.
    /// </summary>
    ArchiveConversion
}

/// <summary>The invoice payload that is embedded into an existing PDF.</summary>
public sealed record PdfEInvoiceAttachmentPayload
{
    public required ReadOnlyMemory<byte> Xml { get; init; }

    public required string FileName { get; init; }

    public string MimeType { get; init; } = "application/xml";

    public PdfAssociatedFileRelationship Relationship { get; init; } =
        PdfAssociatedFileRelationship.Alternative;

    public string? Description { get; init; }

    /// <summary>The PDF/A extension schema describing the embedded invoice, if any.</summary>
    public PdfAssociatedFileXmpMetadata? XmpMetadata { get; init; }
}

/// <summary>Bounds and explicit approvals for an attachment operation.</summary>
public sealed record PdfEInvoiceAttachmentOptions
{
    public const long DefaultMaximumPdfSizeBytes = 100L * 1024 * 1024;
    public const long DefaultMaximumOutputSizeBytes = 120L * 1024 * 1024;

    public long MaximumPdfSizeBytes { get; init; } = DefaultMaximumPdfSizeBytes;

    public long MaximumOutputSizeBytes { get; init; } = DefaultMaximumOutputSizeBytes;

    /// <summary>Approves rewriting a source whose archival identification forbids embedded files.</summary>
    public bool AllowArchiveConversion { get; init; }

    /// <summary>
    /// Approves modifying a signed document. A rewrite invalidates existing signatures; an
    /// incremental attachment preserves their signed bytes but creates a later document revision.
    /// </summary>
    public bool AllowSignatureInvalidation { get; init; }

    /// <summary>Approves dereferencing an existing invoice attachment with the same purpose.</summary>
    public bool ReplaceExistingInvoiceAttachment { get; init; }
}

public sealed record PdfEInvoiceAttachmentPreflightReport(
    string TargetProfile,
    PdfComplianceConversionCapability Capability,
    PdfEInvoiceAttachmentStrategy Strategy,
    string SourceSha256,
    long SourceSizeBytes,
    int PageCount,
    int SignatureCount,
    string? SourceArchiveProfile,
    bool RequiresArchiveConversion,
    bool RequiresApproval,
    bool InvalidatesSignatures,
    bool OriginalPreserved,
    IReadOnlyList<string> ConflictingAttachmentNames,
    long EstimatedOutputSizeBytes,
    IReadOnlyList<PdfComplianceConversionFinding> Findings);

public sealed record PdfEInvoiceAttachmentResult(
    byte[] Bytes,
    string SourceSha256,
    string ResultSha256,
    string AttachmentSha256,
    string FileName,
    long OutputSizeBytes,
    int PageCount,
    bool PagesUnchanged,
    PdfEInvoiceAttachmentStrategy Strategy,
    PdfEInvoiceAttachmentPreflightReport Preflight);

/// <summary>
/// Raised when an attachment cannot proceed. The source document is never modified; the
/// preflight report explains what has to be approved or corrected first.
/// </summary>
public sealed class PdfEInvoiceAttachmentBlockedException(
    string message,
    PdfEInvoiceAttachmentPreflightReport preflight) : InvalidOperationException(message)
{
    public PdfEInvoiceAttachmentPreflightReport Preflight { get; } = preflight;
}

public interface IPdfEInvoiceAttacher
{
    Task<PdfEInvoiceAttachmentPreflightReport> PreflightAsync(
        Stream pdfStream,
        PdfEInvoiceAttachmentPayload payload,
        PdfEInvoiceAttachmentOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<PdfEInvoiceAttachmentResult> AttachAsync(
        Stream pdfStream,
        PdfEInvoiceAttachmentPayload payload,
        PdfEInvoiceAttachmentOptions? options = null,
        CancellationToken cancellationToken = default);
}
