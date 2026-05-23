namespace Canvas.Pdf;

public sealed class PdfSaveOptions
{
    public static PdfSaveOptions Default { get; } = new();

    public bool CompressContentStreams { get; init; }

    public bool CollectDiagnostics { get; init; }
}
