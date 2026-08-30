namespace PXA.Pdf;

/// <summary>
/// Configures password protection and access permissions for a generated PDF using the
/// Standard Security Handler. Supply via <see cref="PdfSaveOptions.Encryption"/>.
/// </summary>
public sealed class PdfEncryptionOptions
{
    /// <summary>
    /// Password required to open the document. When null or empty, the document opens without a
    /// prompt but is still encrypted and subject to <see cref="Permissions"/>.
    /// </summary>
    public string? UserPassword { get; init; }

    /// <summary>
    /// Password that grants full access and the right to change permissions. AES security profiles
    /// require a distinct owner password whenever <see cref="Permissions"/> restricts an operation.
    /// </summary>
    public string? OwnerPassword { get; init; }

    /// <summary>
    /// Operations permitted when the document is opened with the user password.
    /// Defaults to <see cref="PdfPermissions.All"/>.
    /// </summary>
    public PdfPermissions Permissions { get; init; } = PdfPermissions.All;

    /// <summary>
    /// Encryption algorithm. Defaults to the recommended <see cref="PdfEncryptionAlgorithm.Aes256"/>.
    /// </summary>
    public PdfEncryptionAlgorithm Algorithm { get; init; } = PdfEncryptionAlgorithm.Aes256;

    /// <summary>Whether document metadata streams are encrypted. Defaults to <see langword="true"/>.</summary>
    public bool EncryptMetadata { get; init; } = true;
}
