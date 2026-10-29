namespace PVAD.Pdf;

/// <summary>
/// Encryption algorithm used by the Standard Security Handler.
/// </summary>
public enum PdfEncryptionAlgorithm
{
    /// <summary>
    /// AES with a 256-bit key using the PDF 2.0 revision-6 Standard Security Handler.
    /// This is the recommended algorithm and the default for new encrypted documents.
    /// </summary>
    Aes256,

    /// <summary>
    /// Legacy RC4 with a 128-bit key (PDF 32000-1, <c>/V 2 /R 3</c>).
    /// Select only when an older reader cannot open PDF 2.0 AES-256 documents.
    /// </summary>
    Rc4_128,

    /// <summary>
    /// AES with a 128-bit key (PDF 32000-1, <c>/V 4 /R 4</c>, <c>/AESV2</c>).
    /// Select only as an interoperability bridge for readers that cannot open AES-256.
    /// </summary>
    Aes128
}
