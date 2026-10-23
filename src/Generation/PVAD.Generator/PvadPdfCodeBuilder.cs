using PVAD.Core.Contracts;

namespace PVAD.Generator;

/// <summary>
/// Semantic PDF code result. The canonical design is rendered by the trusted PVAD host after the
/// sandbox has evaluated user code.
/// </summary>
public sealed class PvadPdfCodeDocument
{
    public required DesignExportDto Design { get; init; }
}

/// <summary>
/// Builds a PDF-bound design without reducing Designer elements to drawing primitives.
/// </summary>
public sealed class PvadPdfCodeBuilder
{
    private readonly DesignExportDto _design;

    public PvadPdfCodeBuilder(DesignExportDto document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _design = document;
        _design.Pages = [];
    }

    public PvadPdfCodePageBuilder AddPage(PageDto page)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Elements = [];
        _design.Pages.Add(page);
        return new PvadPdfCodePageBuilder(page);
    }

    public PvadPdfCodeDocument Build()
    {
        if (_design.Pages.Count == 0)
            throw new InvalidOperationException("PXACODE140: A PDF code document must contain at least one page.");
        return new PvadPdfCodeDocument { Design = _design };
    }
}

public sealed class PvadPdfCodePageBuilder(PageDto page)
{
    public PvadPdfCodePageBuilder Add(ElementDto element)
    {
        ArgumentNullException.ThrowIfNull(element);
        page.Elements.Add(element);
        return this;
    }
}
