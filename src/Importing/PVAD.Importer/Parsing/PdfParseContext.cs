using PVAD.Importer.Objects;

namespace PVAD.Importer.Parsing;

public sealed class PdfParseContext
{
    private readonly List<PdfParseDiagnostic> _diagnostics = [];

    public PdfParseContext(
        ReadOnlyMemory<byte> source,
        PdfImporterOptions options,
        CancellationToken cancellationToken = default)
    {
        Source = source;
        Options = options;
        CancellationToken = cancellationToken;
    }

    public ReadOnlyMemory<byte> Source { get; }
    public PdfImporterOptions Options { get; }
    public CancellationToken CancellationToken { get; }
    public PdfParseLimits Limits => Options.Limits;
    public Dictionary<PdfObjectId, PdfIndirectObject> ObjectCache { get; } = [];
    public IReadOnlyList<PdfParseDiagnostic> Diagnostics => _diagnostics;

    public void AddDiagnostic(PdfParseDiagnostic diagnostic)
    {
        // A hostile document can trip the same limit on every object; keep the report bounded.
        if (_diagnostics.Count < 256)
        {
            _diagnostics.Add(diagnostic);
        }
    }
}
