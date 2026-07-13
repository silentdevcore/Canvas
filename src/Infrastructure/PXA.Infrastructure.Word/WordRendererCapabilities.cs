using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Word;

public sealed class WordRendererCapabilities : IRendererCapabilities
{
    public string RendererKey => "word";

    public bool SupportsBookmarks => true;

    public bool SupportsTableOfContents => true;

    public bool SupportsNamedDestinations => false;

    public bool SupportsInternalLinks => true;

    public bool SupportsExternalLinks => true;

    public bool SupportsImageOpacity => false;

    public bool SupportsPageRotation => false;

    public bool SupportsPageBoundaries => false;

    public bool SupportsWatermarks => true;

    public bool SupportsHeaderFooter => true;

    public bool SupportsSectionNumbering => true;

    public bool SupportsAdvancedTextDecorations => true;

    public bool SupportsCompression => false;
}
