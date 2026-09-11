using System.Security.Cryptography.X509Certificates;

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

public enum PdfCertificateRevocationMode
{
    NoCheck,
    Offline,
    Online
}

public sealed record PdfSignatureValidationPolicy
{
    public bool UseSystemTrustStore { get; init; } = true;
    public IReadOnlyList<X509Certificate2> CustomTrustAnchors { get; init; } = [];
    public IReadOnlyList<X509Certificate2> IntermediateCertificates { get; init; } = [];
    public PdfCertificateRevocationMode RevocationMode { get; init; } = PdfCertificateRevocationMode.NoCheck;
    public X509RevocationFlag RevocationFlag { get; init; } = X509RevocationFlag.ExcludeRoot;
    public bool AllowCertificateDownloads { get; init; }
    public TimeSpan NetworkTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public IPdfRevocationEvidenceProvider? RevocationEvidenceProvider { get; init; }
    public IPdfIssuerCertificateProvider? IssuerCertificateProvider { get; init; }
}

public sealed record PdfRevocationEvidenceInspection(
    PdfRevocationEvidenceKind Kind,
    PdfTrustStatus Status,
    string Source,
    DateTimeOffset ThisUpdate,
    DateTimeOffset? NextUpdate);

public sealed record PdfSignatureEmbeddedValidationMaterialInspection(
    bool VriMatched,
    int CertificateCount,
    int CrlCount,
    int OcspResponseCount,
    PdfTrustStatus Status);

public enum PdfCertificationTransform
{
    DocMdp,
    FieldMdp
}

public sealed record PdfCertificationInspection(
    int Level,
    PdfTrustStatus Status,
    PdfTrustStatus FieldLockStatus,
    int DisallowedChangeCount,
    IReadOnlyList<string> DisallowedChanges,
    IReadOnlyList<string> LockedFields);

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
    string? TimestampAuthoritySubject = null,
    PdfTrustStatus RevocationStatus = PdfTrustStatus.NotApplicable,
    PdfTrustStatus TimestampRevocationStatus = PdfTrustStatus.NotApplicable,
    IReadOnlyList<PdfRevocationEvidenceInspection>? RevocationEvidence = null,
    IReadOnlyList<PdfRevocationEvidenceInspection>? TimestampRevocationEvidence = null,
    PdfSignatureEmbeddedValidationMaterialInspection? EmbeddedValidationMaterial = null,
    PdfTrustStatus HistoricalRevocationStatus = PdfTrustStatus.NotApplicable,
    PdfTrustStatus TimestampHistoricalRevocationStatus = PdfTrustStatus.NotApplicable,
    bool IsDocumentTimestamp = false,
    PdfTrustStatus ArchiveTimestampStatus = PdfTrustStatus.NotApplicable,
    DateTimeOffset? ArchiveTimestampTime = null,
    string? ArchiveTimestampAuthoritySubject = null,
    bool IsCertification = false,
    PdfTrustStatus CertificationStatus = PdfTrustStatus.NotApplicable,
    PdfCertificationInspection? Certification = null,
    string? FieldName = null,
    int? FieldPageNumber = null,
    IReadOnlyList<double>? FieldRectangle = null);

public sealed record PdfComplianceClaim(
    string Standard,
    string? Profile,
    bool IsClaimed,
    PdfTrustStatus Status,
    string Evidence,
    string? Validator = null,
    DateTimeOffset? ValidatedAtUtc = null);

public sealed record PdfRevisionInspection(
    int SequenceNumber,
    string Kind,
    long CrossReferenceOffset,
    long EndOffset,
    string Sha256,
    int EntryCount);

public sealed record PdfEmbeddedValidationMaterialInspection(
    int CertificateCount,
    int CrlCount,
    int OcspResponseCount,
    int VriEntryCount,
    long TotalBytes,
    PdfTrustStatus Status);

public sealed record PdfRiskInspection(
    int JavaScriptActionCount,
    int LaunchActionCount,
    int EmbeddedFileCount,
    int FileAttachmentCount,
    int RichMediaCount,
    int XfaFormCount,
    int ExternalLinkCount,
    PdfTrustStatus Status)
{
    public bool HasRiskyContent =>
        JavaScriptActionCount > 0 || LaunchActionCount > 0 || EmbeddedFileCount > 0 ||
        FileAttachmentCount > 0 || RichMediaCount > 0 || XfaFormCount > 0;
}

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
    IReadOnlyList<PdfTrustDiagnostic> Diagnostics,
    PdfEmbeddedValidationMaterialInspection? EmbeddedValidationMaterial = null,
    PdfRiskInspection? Risk = null);

public sealed record PdfTrustInspectionOptions
{
    public const long DefaultMaximumFileSizeBytes = 100L * 1024 * 1024;

    public long MaximumFileSizeBytes { get; init; } = DefaultMaximumFileSizeBytes;
    public PdfSignatureValidationPolicy SignatureValidationPolicy { get; init; } = new();
}

public interface IPdfTrustInspector
{
    Task<PdfTrustReport> InspectAsync(
        Stream pdfStream,
        PdfTrustInspectionOptions? options = null,
        CancellationToken cancellationToken = default);
}
