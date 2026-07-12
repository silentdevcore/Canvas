using PXA.Infrastructure.Word;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.Json;

namespace PXA.Export.Tests;

public sealed class WordPageGeometryTests
{
    [Fact]
    public void Word_Export_SetsSectionPageSizeAndMargins_FromPageSettings()
    {
        var design = new DesignExportDto
        {
            Id = "g1",
            Name = "Geometry",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto { Id = "t1", Type = "text", Content = "Hello", Width = 100, Height = 20 }
                    ]
                }
            ],
            PageSettings = new PageSettingsDto
            {
                Width = 595,
                Height = 842,
                Orientation = "portrait",
                Margins = new MarginsDto { Left = 36, Right = 24, Top = 48, Bottom = 30 }
            }
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var mainPart = doc.MainDocumentPart
            ?? throw new InvalidOperationException("Main document part missing.");
        var document = mainPart.Document
            ?? throw new InvalidOperationException("Word document root missing.");
        var body = document.Body
            ?? throw new InvalidOperationException("Word document body missing.");
        var section = body.GetFirstChild<SectionProperties>();

        Assert.NotNull(section);
        var sectionValue = section!;

        var pageSize = sectionValue.GetFirstChild<PageSize>()
            ?? throw new InvalidOperationException("Section page size missing.");
        var pageWidth = pageSize.Width
            ?? throw new InvalidOperationException("Section page width missing.");
        var pageHeight = pageSize.Height
            ?? throw new InvalidOperationException("Section page height missing.");
        var pageOrient = pageSize.Orient
            ?? throw new InvalidOperationException("Section page orientation missing.");
        Assert.Equal((UInt32Value)11900U, pageWidth);
        Assert.Equal((UInt32Value)16840U, pageHeight);
        Assert.Equal(PageOrientationValues.Portrait, pageOrient.Value);

        var margin = sectionValue.GetFirstChild<PageMargin>()
            ?? throw new InvalidOperationException("Section page margin missing.");
        var left = margin.Left ?? throw new InvalidOperationException("Left margin missing.");
        var right = margin.Right ?? throw new InvalidOperationException("Right margin missing.");
        var top = margin.Top ?? throw new InvalidOperationException("Top margin missing.");
        var bottom = margin.Bottom ?? throw new InvalidOperationException("Bottom margin missing.");
        Assert.Equal(720U, left.Value);
        Assert.Equal(480U, right.Value);
        Assert.Equal(960, top.Value);
        Assert.Equal(600, bottom.Value);
    }

    [Fact]
    public void Word_Export_SetsLandscapeOrientation_WhenConfigured()
    {
        var design = new DesignExportDto
        {
            Id = "g2",
            Name = "Landscape",
            Pages = [new PageDto { Id = "p1", Elements = [] }],
            PageSettings = new PageSettingsDto
            {
                Width = 842,
                Height = 595,
                Orientation = "landscape"
            }
        };

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var mainPart = doc.MainDocumentPart
            ?? throw new InvalidOperationException("Main document part missing.");
        var document = mainPart.Document
            ?? throw new InvalidOperationException("Word document root missing.");
        var body = document.Body
            ?? throw new InvalidOperationException("Word document body missing.");
        var section = body.GetFirstChild<SectionProperties>();
        var sectionValue = section ?? throw new InvalidOperationException("Section properties missing.");
        var pageSize = sectionValue.GetFirstChild<PageSize>()
            ?? throw new InvalidOperationException("Section page size missing.");
        var orient = pageSize.Orient ?? throw new InvalidOperationException("Page orientation missing.");
        var width = pageSize.Width ?? throw new InvalidOperationException("Page width missing.");
        var height = pageSize.Height ?? throw new InvalidOperationException("Page height missing.");

        Assert.Equal(PageOrientationValues.Landscape, orient.Value);
        Assert.True(width.Value > height.Value);
    }

    [Fact]
    public void Word_Export_SamplePack_PageGeometryMatchesConfiguredPageBox()
    {
        var repoRoot = FindRepoRoot();
        var samplesDir = Path.Combine(repoRoot, "checklists", "word-fidelity-samples");
        var sampleFiles = Directory.GetFiles(samplesDir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(sampleFiles);

        foreach (var sampleFile in sampleFiles)
        {
            var json = File.ReadAllText(sampleFile);
            var design = JsonSerializer.Deserialize<DesignExportDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(design);
            var bytes = new WordDocumentExporter().Export(design!);

            using var ms = new MemoryStream(bytes);
            using var doc = WordprocessingDocument.Open(ms, false);
            var section = doc.MainDocumentPart!.Document!.Body!.GetFirstChild<SectionProperties>();
            Assert.NotNull(section);

            var pageSize = section!.GetFirstChild<PageSize>();
            Assert.NotNull(pageSize);

            var expectedWidth = design!.PageSettings?.Width > 0 ? design.PageSettings.Width : 595;
            var expectedHeight = design.PageSettings?.Height > 0 ? design.PageSettings.Height : 842;
            var expectedLandscape = string.Equals(design.PageSettings?.Orientation, "landscape", StringComparison.OrdinalIgnoreCase);

            var expectedWidthTwips = CanvasToTwips(expectedWidth);
            var expectedHeightTwips = CanvasToTwips(expectedHeight);
            if (expectedLandscape && expectedWidthTwips < expectedHeightTwips)
                (expectedWidthTwips, expectedHeightTwips) = (expectedHeightTwips, expectedWidthTwips);
            else if (!expectedLandscape && expectedWidthTwips > expectedHeightTwips)
                (expectedWidthTwips, expectedHeightTwips) = (expectedHeightTwips, expectedWidthTwips);

            Assert.Equal((uint)expectedWidthTwips, pageSize!.Width!.Value);
            Assert.Equal((uint)expectedHeightTwips, pageSize.Height!.Value);
        }
    }

    private static int CanvasToTwips(double units)
        => (int)Math.Round(units * 1440.0 / 72.0, MidpointRounding.AwayFromZero);

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "PXA.sln")))
                return dir;

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
