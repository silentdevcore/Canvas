using System.Security.Cryptography.X509Certificates;

namespace PXA.Pdf;

/// <summary>Configures X.509 recipient encryption for a generated PDF.</summary>
public sealed class PdfCertificateEncryptionOptions
{
    /// <summary>Certificates whose corresponding private keys may open the document.</summary>
    public required IReadOnlyList<X509Certificate2> Recipients { get; init; }

    /// <summary>Operations granted to every recipient in this profile.</summary>
    public PdfPermissions Permissions { get; init; } = PdfPermissions.All;

    /// <summary>Whether the document-level metadata stream is encrypted.</summary>
    public bool EncryptMetadata { get; init; } = true;
}
