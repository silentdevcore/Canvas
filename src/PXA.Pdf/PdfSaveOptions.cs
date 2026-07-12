namespace PXA.Pdf;

public sealed class PdfSaveOptions
{
    public static PdfSaveOptions Default { get; } = new();

    public bool CompressContentStreams { get; init; }

    public bool CollectDiagnostics { get; init; }

    /// <summary>
    /// When set, the document is encrypted with the Standard Security Handler using these settings.
    /// When null (the default), the document is written unencrypted.
    /// </summary>
    public PdfEncryptionOptions? Encryption { get; init; }
}
