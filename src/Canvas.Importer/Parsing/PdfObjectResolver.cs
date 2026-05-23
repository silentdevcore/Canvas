using Canvas.Importer.Fonts;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Parsing;

public sealed class PdfObjectResolver : IPdfObjectResolver
{
    private readonly PdfObjectGraph _graph;

    public PdfObjectResolver(PdfObjectGraph graph)
    {
        _graph = graph;
    }

    public PdfObject? Resolve(PdfObject value)
    {
        return value is PdfReference reference ? _graph.Resolve(reference.Id)?.Value : value;
    }

    public bool TryResolve<TObject>(PdfObject? value, out TObject resolved)
        where TObject : PdfObject
    {
        if (value is not null && Resolve(value) is TObject typed)
        {
            resolved = typed;
            return true;
        }

        resolved = null!;
        return false;
    }

    public PdfIndirectObject? ResolveIndirect(PdfReference reference) => _graph.Resolve(reference.Id);
}
