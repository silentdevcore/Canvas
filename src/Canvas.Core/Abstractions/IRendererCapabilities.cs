namespace Canvas.Core.Abstractions;

public interface IRendererCapabilities
{
    string RendererKey { get; }

    bool SupportsBookmarks { get; }

    bool SupportsTableOfContents { get; }

    bool SupportsNamedDestinations { get; }

    bool SupportsInternalLinks { get; }

    bool SupportsExternalLinks { get; }

    bool SupportsImageOpacity { get; }

    bool SupportsPageRotation { get; }

    bool SupportsPageBoundaries { get; }

    bool SupportsWatermarks { get; }

    bool SupportsHeaderFooter { get; }

    bool SupportsSectionNumbering { get; }

    bool SupportsAdvancedTextDecorations { get; }

    bool SupportsCompression { get; }
}
