namespace PXA.Pdf;

public sealed class PdfSaveOptions
{
    public static PdfSaveOptions Default { get; } = new();

    public bool CompressContentStreams { get; init; }

    public bool CollectDiagnostics { get; init; }

    /// <summary>
    /// Optional archival compliance profile. Compliance output is validated before it is written;
    /// unsupported document features cause <see cref="PdfComplianceException"/> instead of a
    /// misleading conformance claim.
    /// </summary>
    public PdfComplianceProfile ComplianceProfile { get; init; } = PdfComplianceProfile.None;

    /// <summary>
    /// When set, the document is encrypted with the Standard Security Handler using these settings.
    /// When null (the default), the document is written unencrypted.
    /// </summary>
    public PdfEncryptionOptions? Encryption { get; init; }

    /// <summary>
    /// When set, the document is encrypted for the listed X.509 recipients using the
    /// interoperable <c>Adobe.PubSec</c> AES-128 certificate profile. This option is mutually
    /// exclusive with <see cref="Encryption"/>.
    /// </summary>
    public PdfCertificateEncryptionOptions? CertificateEncryption { get; init; }
}
