namespace Canvas.Pdf;

/// <summary>
/// Encryption algorithm used by the Standard Security Handler.
/// </summary>
public enum PdfEncryptionAlgorithm
{
    /// <summary>
    /// RC4 with a 128-bit key (PDF 32000-1, <c>/V 2 /R 3</c>). Widely supported by readers.
    /// </summary>
    Rc4_128,

    /// <summary>
    /// AES with a 128-bit key (PDF 32000-1, <c>/V 4 /R 4</c>, <c>/AESV2</c>).
    /// Reserved for a future release; not yet implemented.
    /// </summary>
    Aes128
}
