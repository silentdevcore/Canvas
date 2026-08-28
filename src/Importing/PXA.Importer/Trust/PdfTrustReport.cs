namespace PXA.Importer.Trust;

public enum PdfTrustStatus
{
    Valid,
    Invalid,
    Indeterminate,
    Unsupported,
    NotApplicable
}

public enum PdfTrustDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record PdfTrustDiagnostic(
    string Code,
    PdfTrustDiagnosticSeverity Severity,
    string Message,
    int? ObjectNumber = null,
    int? Generation = null);

public sealed record PdfEncryptionInspection(
    bool IsEncrypted,
    PdfTrustStatus Status,
    string? Filter = null,
    string? Algorithm = null,
    int? Version = null,
    int? Revision = null,
    int? KeyLengthBits = null);

public sealed record PdfSignatureInspection(
    int ObjectNumber,
    int Generation,
    PdfTrustStatus Status,
    string? SubFilter,
    IReadOnlyList<long> ByteRange,
    int? ContentsLengthBytes,
    PdfTrustStatus IntegrityStatus = PdfTrustStatus.Indeterminate,
    PdfTrustStatus CoverageStatus = PdfTrustStatus.Indeterminate,
    PdfTrustStatus CertificateStatus = PdfTrustStatus.Indeterminate,
    string? PadesProfile = null,
    string? DigestAlgorithm = null,
    string? SignerSubject = null,
    string? SignerThumbprint = null,
    DateTimeOffset? SigningTime = null,
    bool CoversWholeDocument = false,
    long? SignedRevisionEndOffset = null,
    PdfTrustStatus TimestampStatus = PdfTrustStatus.NotApplicable,
    DateTimeOffset? TimestampTime = null,
    string? TimestampAuthoritySubject = null);

public sealed record PdfComplianceClaim(
    string Standard,
    string? Profile,
    bool IsClaimed,
    PdfTrustStatus Status,
    string Evidence);

public sealed record PdfRevisionInspection(
    int SequenceNumber,
    string Kind,
    long CrossReferenceOffset,
    long EndOffset,
    string Sha256,
    int EntryCount);

public sealed record PdfTrustReport(
    PdfTrustStatus Status,
    PdfTrustStatus StructureStatus,
    string Sha256,
    long FileSizeBytes,
    string? PdfVersion,
    int RevisionCount,
    int ObjectCount,
    PdfEncryptionInspection Encryption,
    IReadOnlyList<PdfSignatureInspection> Signatures,
    PdfComplianceClaim PdfA,
    PdfComplianceClaim PdfUa,
    IReadOnlyList<PdfRevisionInspection> Revisions,
    IReadOnlyList<PdfTrustDiagnostic> Diagnostics);

public sealed record PdfTrustInspectionOptions
{
    public const long DefaultMaximumFileSizeBytes = 100L * 1024 * 1024;

    public long MaximumFileSizeBytes { get; init; } = DefaultMaximumFileSizeBytes;
}

public interface IPdfTrustInspector
{
    Task<PdfTrustReport> InspectAsync(
        Stream pdfStream,
        PdfTrustInspectionOptions? options = null,
        CancellationToken cancellationToken = default);
}
