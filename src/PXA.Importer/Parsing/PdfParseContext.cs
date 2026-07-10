using PXA.Importer.Objects;

namespace PXA.Importer.Parsing;

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
