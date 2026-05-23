using Canvas.Core.Abstractions;

namespace Canvas.Core.Capabilities;

public static class RendererCapabilityFallback
{
    public static bool TryEnsureSupported(
        IRendererCapabilities capabilities,
        RendererFeature feature,
        UnsupportedFeatureFallbackMode fallbackMode,
        out string? reason)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        if (IsSupported(capabilities, feature))
        {
            reason = null;
            return true;
        }

        reason = $"Renderer '{capabilities.RendererKey}' does not support feature '{feature}'.";

        if (fallbackMode == UnsupportedFeatureFallbackMode.Throw)
        {
            throw new NotSupportedException(reason);
        }

        return false;
    }

    public static bool IsSupported(IRendererCapabilities capabilities, RendererFeature feature)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return feature switch
        {
            RendererFeature.Bookmarks => capabilities.SupportsBookmarks,
            RendererFeature.TableOfContents => capabilities.SupportsTableOfContents,
            RendererFeature.NamedDestinations => capabilities.SupportsNamedDestinations,
            RendererFeature.InternalLinks => capabilities.SupportsInternalLinks,
            RendererFeature.ExternalLinks => capabilities.SupportsExternalLinks,
            RendererFeature.ImageOpacity => capabilities.SupportsImageOpacity,
            RendererFeature.PageRotation => capabilities.SupportsPageRotation,
            RendererFeature.PageBoundaries => capabilities.SupportsPageBoundaries,
            RendererFeature.Watermarks => capabilities.SupportsWatermarks,
            RendererFeature.HeaderFooter => capabilities.SupportsHeaderFooter,
            RendererFeature.SectionNumbering => capabilities.SupportsSectionNumbering,
            RendererFeature.AdvancedTextDecorations => capabilities.SupportsAdvancedTextDecorations,
            RendererFeature.Compression => capabilities.SupportsCompression,
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, "Unknown renderer feature.")
        };
    }
}
