using PXA.Core.Abstractions;
using PXA.Core.Capabilities;
using PXA.Core.Contracts;

namespace PXA.Core.Tests;

public sealed class CapabilityAdaptersTests
{
    [Theory]
    [InlineData(RendererFeature.Bookmarks)]
    [InlineData(RendererFeature.TableOfContents)]
    [InlineData(RendererFeature.NamedDestinations)]
    [InlineData(RendererFeature.InternalLinks)]
    [InlineData(RendererFeature.ExternalLinks)]
    [InlineData(RendererFeature.ImageOpacity)]
    [InlineData(RendererFeature.PageRotation)]
    [InlineData(RendererFeature.PageBoundaries)]
    [InlineData(RendererFeature.Watermarks)]
    [InlineData(RendererFeature.HeaderFooter)]
    [InlineData(RendererFeature.SectionNumbering)]
    [InlineData(RendererFeature.AdvancedTextDecorations)]
    [InlineData(RendererFeature.Compression)]
    public void RendererFeature_MapsByName(RendererFeature feature)
    {
        var canvasFeature = feature.ToCanvas();
        var roundTrip = canvasFeature.ToPxa();

        Assert.Equal(feature.ToString(), canvasFeature.ToString());
        Assert.Equal(feature, roundTrip);
    }

    [Theory]
    [InlineData(UnsupportedFeatureFallbackMode.Throw)]
    [InlineData(UnsupportedFeatureFallbackMode.Skip)]
    public void UnsupportedFeatureFallbackMode_MapsByName(UnsupportedFeatureFallbackMode mode)
    {
        var canvasMode = mode.ToCanvas();
        var roundTrip = canvasMode.ToPxa();

        Assert.Equal(mode.ToString(), canvasMode.ToString());
        Assert.Equal(mode, roundTrip);
    }

    [Fact]
    public void TryEnsureSupported_ReturnsFalse_WhenUnsupportedAndModeIsSkip()
    {
        var capabilities = new TestCapabilities();

        var supported = RendererCapabilityFallback.TryEnsureSupported(
            capabilities,
            RendererFeature.TableOfContents,
            UnsupportedFeatureFallbackMode.Skip,
            out var reason);

        Assert.False(supported);
        Assert.NotNull(reason);
    }

    [Fact]
    public void TryEnsureSupported_Throws_WhenUnsupportedAndModeIsThrow()
    {
        var capabilities = new TestCapabilities();

        Assert.Throws<NotSupportedException>(() => RendererCapabilityFallback.TryEnsureSupported(
            capabilities,
            RendererFeature.TableOfContents,
            UnsupportedFeatureFallbackMode.Throw,
            out _));
    }

    [Fact]
    public void IsSupported_ReturnsTrue_WhenCapabilityExists()
    {
        var capabilities = new TestCapabilities();

        var supported = RendererCapabilityFallback.IsSupported(capabilities, RendererFeature.ExternalLinks);

        Assert.True(supported);
    }

    private sealed class TestCapabilities : IRendererCapabilities
    {
        public string RendererKey => "test";
        public bool SupportsBookmarks => true;
        public bool SupportsTableOfContents => false;
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
}
