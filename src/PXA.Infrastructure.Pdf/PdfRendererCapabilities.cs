using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Pdf;

public sealed class PdfRendererCapabilities : IRendererCapabilities
{
    public string RendererKey => "pdf";

    public bool SupportsBookmarks => true;

    public bool SupportsTableOfContents => true;

    public bool SupportsNamedDestinations => true;

    public bool SupportsInternalLinks => true;

    public bool SupportsExternalLinks => true;

    public bool SupportsImageOpacity => true;

    public bool SupportsPageRotation => true;

    public bool SupportsPageBoundaries => true;

    public bool SupportsWatermarks => true;

    public bool SupportsHeaderFooter => true;

    public bool SupportsSectionNumbering => true;

    public bool SupportsAdvancedTextDecorations => true;

    public bool SupportsCompression => true;
}
