namespace PXA.Infrastructure.Pdf;

/// <summary>
/// Power Dox Automation PDF renderer capability snapshot.
/// </summary>
public sealed class PdfRendererCapabilities
{
    private readonly Canvas.Infrastructure.Pdf.PdfRendererCapabilities inner = new();

    public string RendererKey => inner.RendererKey;
    public bool SupportsBookmarks => inner.SupportsBookmarks;
    public bool SupportsTableOfContents => inner.SupportsTableOfContents;
    public bool SupportsNamedDestinations => inner.SupportsNamedDestinations;
    public bool SupportsInternalLinks => inner.SupportsInternalLinks;
    public bool SupportsExternalLinks => inner.SupportsExternalLinks;
    public bool SupportsImageOpacity => inner.SupportsImageOpacity;
    public bool SupportsPageRotation => inner.SupportsPageRotation;
    public bool SupportsPageBoundaries => inner.SupportsPageBoundaries;
    public bool SupportsWatermarks => inner.SupportsWatermarks;
    public bool SupportsHeaderFooter => inner.SupportsHeaderFooter;
    public bool SupportsSectionNumbering => inner.SupportsSectionNumbering;
    public bool SupportsAdvancedTextDecorations => inner.SupportsAdvancedTextDecorations;
    public bool SupportsCompression => inner.SupportsCompression;
}
