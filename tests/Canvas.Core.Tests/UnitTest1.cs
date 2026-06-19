using Canvas.Core.Abstractions;
using Canvas.Core.Capabilities;
using Canvas.Core.Contracts;
using Canvas.Core.Primitives;

namespace Canvas.Core.Tests;

public class CorePrimitivesTests
{
    [Fact]
    public void PdfPoint_ShouldExposeConstructorValues()
    {
        var point = new PdfPoint(12.5, 34.75);

        Assert.Equal(12.5, point.X);
        Assert.Equal(34.75, point.Y);
    }

    [Fact]
    public void PdfTextAlignment_ShouldContainExpectedValues()
    {
        Assert.Equal(0, (int)PdfTextAlignment.Left);
        Assert.Equal(1, (int)PdfTextAlignment.Center);
        Assert.Equal(2, (int)PdfTextAlignment.Right);
        Assert.Equal(3, (int)PdfTextAlignment.Justify);
    }

    [Fact]
    public void PdfVerticalAlignment_ShouldContainExpectedValues()
    {
        Assert.Equal(0, (int)PdfVerticalAlignment.Top);
        Assert.Equal(1, (int)PdfVerticalAlignment.Middle);
        Assert.Equal(2, (int)PdfVerticalAlignment.Bottom);
    }
}

public class RendererCapabilityFallbackTests
{
    [Fact]
    public void TryEnsureSupported_ShouldReturnFalse_WhenUnsupportedAndModeIsSkip()
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
    public void TryEnsureSupported_ShouldThrow_WhenUnsupportedAndModeIsThrow()
    {
        var capabilities = new TestCapabilities();

        Assert.Throws<NotSupportedException>(() => RendererCapabilityFallback.TryEnsureSupported(
            capabilities,
            RendererFeature.TableOfContents,
            UnsupportedFeatureFallbackMode.Throw,
            out _));
    }

    [Fact]
    public void IsSupported_ShouldReturnTrue_WhenCapabilityExists()
    {
        var capabilities = new TestCapabilities();

        var supported = RendererCapabilityFallback.IsSupported(capabilities, RendererFeature.ExternalLinks);

        Assert.True(supported);
    }
}

public class DesignLayoutPlannerTests
{
    [Fact]
    public void BuildPages_ShouldCreateFallbackPage_WhenPagesMissing()
    {
        var design = new DesignExportDto
        {
            Id = "d1",
            Name = "Fallback",
            Pages = [],
            SharedElements =
            [
                new ElementDto { Id = "s1", Type = "text", Content = "Shared" }
            ]
        };

        var planned = DesignLayoutPlanner.BuildPages(design);

        Assert.Single(planned);
        Assert.Single(planned[0].Elements);
        Assert.Equal("s1", planned[0].Elements[0].Id);
    }

    [Fact]
    public void BuildPages_ShouldDeduplicateSharedElementById_AndFilterHidden()
    {
        var design = new DesignExportDto
        {
            Id = "d2",
            Name = "Dedup",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto { Id = "a", Type = "text", X = 20, Y = 20 },
                        new ElementDto { Id = "hidden", Type = "text", Hidden = true, X = 30, Y = 30 }
                    ]
                }
            ],
            SharedElements =
            [
                new ElementDto { Id = "a", Type = "text", X = 10, Y = 10 },
                new ElementDto { Id = "b", Type = "text", X = 15, Y = 15 }
            ]
        };

        var planned = DesignLayoutPlanner.BuildPages(design);

        Assert.Single(planned);
        var ids = planned[0].Elements.Select(e => e.Id).ToList();
        Assert.Equal(["b", "a"], ids);
        Assert.DoesNotContain("hidden", ids);
    }

    [Fact]
    public void BuildPages_ShouldUseDeterministicTieBreakersIncludingZIndexAndId()
    {
        var design = new DesignExportDto
        {
            Id = "d3",
            Name = "Order",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto { Id = "c", Type = "text", X = 10, Y = 10, Style = new() { ["zIndex"] = 2 } },
                        new ElementDto { Id = "a", Type = "text", X = 10, Y = 10, Style = new() { ["zIndex"] = 1 } },
                        new ElementDto { Id = "b", Type = "text", X = 10, Y = 10, Style = new() { ["zIndex"] = 1 } }
                    ]
                }
            ]
        };

        var planned = DesignLayoutPlanner.BuildPages(design, e =>
        {
            var style = e.Style ?? [];
            return style.TryGetValue("zIndex", out var v) ? Convert.ToInt32(v) : 0;
        });

        var ids = planned[0].Elements.Select(e => e.Id).ToList();
        Assert.Equal(["a", "b", "c"], ids);
    }

    [Fact]
    public void BuildPages_ShouldExpandRepeatElements_FromCustomPropertyJsonArray()
    {
        var design = new DesignExportDto
        {
            Id = "d4",
            Name = "Repeat",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "detail",
                            Type = "table",
                            X = 10,
                            Y = 20,
                            Width = 200,
                            Height = 40,
                            Repeat = new RepeatDto { DataPath = "CategoryGroup", TemplateId = "detail" },
                            CellData =
                            [
                                ["Product", "Qty"],
                                ["{{Product}}", "{{Quantity}}"]
                            ]
                        }
                    ]
                }
            ],
            PageSettings = new PageSettingsDto
            {
                CustomProperties =
                [
                    new CustomDocumentPropertyDto
                    {
                        Name = "CategoryGroup",
                        Value = """[{"Product":"Coffee","Quantity":2},{"Product":"Tea","Quantity":5}]"""
                    }
                ]
            }
        };

        var planned = DesignLayoutPlanner.BuildPages(design);

        var elements = planned[0].Elements.ToList();
        Assert.Equal(2, elements.Count);
        Assert.Equal("detail__repeat_0", elements[0].Id);
        Assert.Equal("Coffee", elements[0].CellData![1][0]);
        Assert.Equal("2", elements[0].CellData![1][1]);
        Assert.Equal(20, elements[0].Y);
        Assert.Null(elements[0].Repeat);
        Assert.Equal("detail__repeat_1", elements[1].Id);
        Assert.Equal("Tea", elements[1].CellData![1][0]);
        Assert.Equal("5", elements[1].CellData![1][1]);
        Assert.Equal(60, elements[1].Y);
    }

    [Fact]
    public void BuildPages_ShouldKeepRepeatTemplate_WhenPayloadIsMissing()
    {
        var design = new DesignExportDto
        {
            Id = "d5",
            Name = "Repeat Missing Payload",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "detail",
                            Type = "text",
                            Content = "{{Product}}",
                            Repeat = new RepeatDto { DataPath = "MissingRows", TemplateId = "detail" }
                        }
                    ]
                }
            ]
        };

        var planned = DesignLayoutPlanner.BuildPages(design);

        var element = Assert.Single(planned[0].Elements);
        Assert.Equal("detail", element.Id);
        Assert.Equal("{{Product}}", element.Content);
        Assert.NotNull(element.Repeat);
    }
}

file sealed class TestCapabilities : IRendererCapabilities
{
    public string RendererKey => "test";

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
