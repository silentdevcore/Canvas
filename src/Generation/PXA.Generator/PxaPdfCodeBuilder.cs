using PXA.Core.Contracts;

namespace PXA.Generator;

/// <summary>
/// Semantic PDF code result. The canonical design is rendered by the trusted PXA host after the
/// sandbox has evaluated user code.
/// </summary>
public sealed class PxaPdfCodeDocument
{
    public required DesignExportDto Design { get; init; }
}

/// <summary>
/// Builds a PDF-bound design without reducing Designer elements to drawing primitives.
/// </summary>
public sealed class PxaPdfCodeBuilder
{
    private readonly DesignExportDto _design;

    public PxaPdfCodeBuilder(DesignExportDto document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _design = document;
        _design.Pages = [];
    }

    public PxaPdfCodePageBuilder AddPage(PageDto page)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Elements = [];
        _design.Pages.Add(page);
        return new PxaPdfCodePageBuilder(page);
    }

    public PxaPdfCodeDocument Build()
    {
        if (_design.Pages.Count == 0)
            throw new InvalidOperationException("PXACODE140: A PDF code document must contain at least one page.");
        return new PxaPdfCodeDocument { Design = _design };
    }
}

public sealed class PxaPdfCodePageBuilder(PageDto page)
{
    public PxaPdfCodePageBuilder Add(ElementDto element)
    {
        ArgumentNullException.ThrowIfNull(element);
        page.Elements.Add(element);
        return this;
    }
}
