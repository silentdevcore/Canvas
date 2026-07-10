using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Spreadsheet;

public sealed class SheetRendererCapabilities : IRendererCapabilities
{
    public string RendererKey => "sheet";

    public bool SupportsBookmarks => false;

    public bool SupportsTableOfContents => false;

    public bool SupportsNamedDestinations => false;

    public bool SupportsInternalLinks => true;

    public bool SupportsExternalLinks => true;

    public bool SupportsImageOpacity => false;

    public bool SupportsPageRotation => false;

    public bool SupportsPageBoundaries => false;

    public bool SupportsWatermarks => false;

    public bool SupportsHeaderFooter => true;

    public bool SupportsSectionNumbering => false;

    public bool SupportsAdvancedTextDecorations => false;

    public bool SupportsCompression => false;
}
