using PXA.Core.Abstractions;

namespace PXA.Infrastructure.Converters;

public sealed class ConverterRendererCapabilities : IRendererCapabilities
{
    public string RendererKey => "converter";

    public bool SupportsBookmarks => false;

    public bool SupportsTableOfContents => false;

    public bool SupportsNamedDestinations => false;

    public bool SupportsInternalLinks => false;

    public bool SupportsExternalLinks => false;

    public bool SupportsImageOpacity => false;

    public bool SupportsPageRotation => false;

    public bool SupportsPageBoundaries => false;

    public bool SupportsWatermarks => false;

    public bool SupportsHeaderFooter => false;

    public bool SupportsSectionNumbering => false;

    public bool SupportsAdvancedTextDecorations => false;

    public bool SupportsCompression => false;
}
