namespace PXA.Importer.Security;

/// <summary>Data encryption algorithm used by the Standard Security Handler.</summary>
public enum PdfDocumentEncryptionAlgorithm
{
    /// <summary>AES-256 (PDF 2.0, <c>/V 5 /R 6</c>, <c>/AESV3</c>).</summary>
    Aes256
}

/// <summary>
/// Access permissions granted when a document is opened with the user password. The owner
/// password always grants full access.
/// </summary>
[Flags]
public enum PdfDocumentPermissions
{
    None = 0,
    Print = 1 << 2,
    ModifyContents = 1 << 3,
    Copy = 1 << 4,
    ModifyAnnotations = 1 << 5,
    FillForms = 1 << 8,
    ExtractForAccessibility = 1 << 9,
    AssembleDocument = 1 << 10,
    PrintHighResolution = 1 << 11,
    All = Print | ModifyContents | Copy | ModifyAnnotations | FillForms |
        ExtractForAccessibility | AssembleDocument | PrintHighResolution
}

/// <summary>Which password opened a document.</summary>
public enum PdfPasswordKind
{
    None,
    User,
    Owner
}

public sealed record PdfDocumentEncryptionOptions
{
    /// <summary>
    /// Password required to open the document. Empty means the document opens without a
    /// prompt. Restricted permissions additionally require a distinct owner password.
    /// </summary>
    public string UserPassword { get; init; } = string.Empty;

    /// <summary>
    /// Password granting full access. It may be omitted only when all permissions are granted.
    /// </summary>
    public string? OwnerPassword { get; init; }

    public PdfDocumentPermissions Permissions { get; init; } = PdfDocumentPermissions.All;

    public PdfDocumentEncryptionAlgorithm Algorithm { get; init; } = PdfDocumentEncryptionAlgorithm.Aes256;

    /// <summary>When false, document metadata streams are left in clear text.</summary>
    public bool EncryptMetadata { get; init; } = true;
}

/// <summary>The outcome of authenticating against an encrypted document.</summary>
public sealed record PdfDecryptionResult(
    PdfPasswordKind PasswordKind,
    PdfDocumentPermissions Permissions,
    bool EncryptMetadata,
    bool PermissionsVerified);

/// <summary>Thrown when a document cannot be opened with the supplied password.</summary>
public sealed class PdfPasswordException(string message) : Exception(message);

/// <summary>Thrown when authenticated encryption metadata fails its integrity checks.</summary>
public sealed class PdfEncryptionIntegrityException(string message) : Exception(message);
