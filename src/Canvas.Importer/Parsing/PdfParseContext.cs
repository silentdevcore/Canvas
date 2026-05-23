using Canvas.Importer.Objects;

namespace Canvas.Importer.Parsing;

public sealed class PdfParseContext
{
    public PdfParseContext(ReadOnlyMemory<byte> source, PdfImporterOptions options)
    {
        Source = source;
        Options = options;
    }

    public ReadOnlyMemory<byte> Source { get; }
    public PdfImporterOptions Options { get; }
    public Dictionary<PdfObjectId, PdfIndirectObject> ObjectCache { get; } = [];
}
