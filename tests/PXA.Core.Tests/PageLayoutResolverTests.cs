using PXA.Core.Contracts;
using PXA.Core.Primitives;

namespace PXA.Core.Tests;

public sealed class PageLayoutResolverTests
{
    [Fact]
    public void Resolve_InheritsDocumentDefaults_AndNormalizesOrientation()
    {
        var defaults = new PageSettingsDto
        {
            Width = 595,
            Height = 842,
            Orientation = "portrait",
            BackgroundColor = "#ffffff",
            Margins = new MarginsDto { Top = 20, Right = 21, Bottom = 22, Left = 23 },
        };

        var inherited = PageLayoutResolver.Resolve(defaults, null);
        var landscape = PageLayoutResolver.Resolve(defaults, new PageLayoutDto
        {
            Orientation = "landscape",
            BackgroundColor = "#ffeecc",
        });

        Assert.Equal(595, inherited.Width);
        Assert.Equal(842, inherited.Height);
        Assert.Equal(842, landscape.Width);
        Assert.Equal(595, landscape.Height);
        Assert.Equal("#ffeecc", landscape.BackgroundColor);
        Assert.Equal(23, landscape.Margins!.Left);
    }

    [Fact]
    public void BuildPages_ResolvesIndependentPhysicalLayouts()
    {
        var design = new DesignExportDto
        {
            Id = "mixed",
            Name = "Mixed layouts",
            PageSettings = new PageSettingsDto { Width = 595, Height = 842, Orientation = "portrait" },
            Pages =
            [
                new PageDto { Id = "portrait" },
                new PageDto { Id = "landscape", Layout = new PageLayoutDto { Width = 842, Height = 595, Orientation = "landscape" } },
                new PageDto { Id = "custom", Layout = new PageLayoutDto { Width = 400, Height = 400, BackgroundColor = "#abcdef", BleedSize = 9 } },
            ],
        };

        var pages = DesignLayoutPlanner.BuildPages(design);

        Assert.Collection(pages,
            page => Assert.Equal((595d, 842d), (page.Layout.Width!.Value, page.Layout.Height!.Value)),
            page => Assert.Equal((842d, 595d), (page.Layout.Width!.Value, page.Layout.Height!.Value)),
            page =>
            {
                Assert.Equal((400d, 400d), (page.Layout.Width!.Value, page.Layout.Height!.Value));
                Assert.Equal("#abcdef", page.Layout.BackgroundColor);
                Assert.Equal(9, page.Layout.BleedSize);
            });
    }

    [Fact]
    public void BuildPages_ReportsStableNormalizationDiagnostics()
    {
        var design = new DesignExportDto
        {
            Id = "invalid",
            Name = "Invalid layout",
            Pages =
            [
                new PageDto
                {
                    Id = "bad-page",
                    Layout = new PageLayoutDto
                    {
                        Width = -1,
                        Height = 842,
                        Orientation = "sideways",
                        Margins = new MarginsDto { Left = -5 },
                    },
                },
            ],
        };

        _ = DesignLayoutPlanner.BuildPages(design);

        Assert.Contains(design.ImportDiagnostics!, item => item.Code == "PXALAYOUT001");
        Assert.Contains(design.ImportDiagnostics!, item => item.Code == "PXALAYOUT003");
        Assert.Contains(design.ImportDiagnostics!, item => item.Code == "PXALAYOUT005");
    }
}
