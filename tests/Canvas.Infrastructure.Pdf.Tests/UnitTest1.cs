using Canvas.Infrastructure.Pdf;
using Canvas.Pdf;
using System.Security.Cryptography;

namespace Canvas.Infrastructure.Pdf.Tests;

public class PdfDocumentRendererTests
{
    [Fact]
    public void Render_ShouldThrow_WhenModelIsNotPdfDocument()
    {
        var sut = new PdfDocumentRenderer();

        Assert.Throws<ArgumentException>(() => sut.Render(new object()));
    }

    [Fact]
    public void Render_ShouldReturnBytes_ForPdfDocument()
    {
        var sut = new PdfDocumentRenderer();
        var document = new PdfDocument();
        document.AddPage().DrawText("hello", 100, 700, 12);

        var bytes = sut.Render(document);

        Assert.NotEmpty(bytes);
    }
}

public class PdfGoldenSnapshotTests
{
    [Fact]
    public void ToBytes_ShouldMatchGoldenHash_ForRepresentativeDocument()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.DrawText("Snapshot Title", 100, 720, 16);
        page.DrawRectangle(90, 680, 220, 28, fill: true, fillColor: new PdfGrayColor(0.95));
        page.DrawText("Snapshot body", 100, 690, 11);
        page.AddWebLink(100, 688, 100, 14, "https://example.com/snapshot");

        var bytes = document.ToBytes(new PdfSaveOptions
        {
            CompressContentStreams = false,
            CollectDiagnostics = false
        });

        var actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        const string expectedHash = "c755ea7c20139f17f1873e1f2e28b1514ecfceefef1adad35ec34418a7b5d214";

        Assert.Equal(expectedHash, actualHash);
    }
}

public class PdfSerializationIntegrationTests
{
    [Fact]
    public void ToBytes_ShouldEmitExpectedPdfMarkers_ForLinksAndPages()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.DrawText("integration", 100, 700, 12);
        page.AddWebLink(100, 680, 120, 16, "https://example.com");

        var bytes = document.ToBytes();
        var content = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.StartsWith("%PDF-", content, StringComparison.Ordinal);
        Assert.Contains("/Type /Catalog", content, StringComparison.Ordinal);
        Assert.Contains("/Type /Page", content, StringComparison.Ordinal);
        Assert.Contains("/Annots", content, StringComparison.Ordinal);
        Assert.Contains("/URI (https://example.com)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ToBytes_WithDiagnosticsEnabled_ShouldPopulateExpectedCounters()
    {
        var document = new PdfDocument();
        var firstPage = document.AddPage();
        var secondPage = document.AddPage();
        firstPage.DrawText("hello", 100, 700, 12);
        firstPage.AddWebLink(100, 680, 80, 12, "https://example.com");
        secondPage.DrawRectangle(100, 500, 60, 30);

        _ = document.ToBytes(new PdfSaveOptions { CollectDiagnostics = true });
        var diagnostics = document.LastDiagnostics;

        Assert.NotNull(diagnostics);
        Assert.Equal(2, diagnostics!.PageCount);
        Assert.Equal(1, diagnostics.PagesWithTextCount);
        Assert.Equal(1, diagnostics.PagesWithLinkCount);
        Assert.Equal(1, diagnostics.PagesWithWebLinkCount);
        Assert.Equal(1, diagnostics.PagesWithShapeCount);
        Assert.Equal(1, diagnostics.WebLinkAnnotationCount);
    }
}

public class PdfColorTests
{
    [Fact]
    public void FromRgb_ShouldNormalizeByteComponents()
    {
        var color = PdfColor.FromRgb(128, 64, 32);

        Assert.Equal(128 / 255d, color.Red);
        Assert.Equal(64 / 255d, color.Green);
        Assert.Equal(32 / 255d, color.Blue);
    }

    [Fact]
    public void FromRgb_ShouldThrow_WhenComponentIsOutsideByteRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfColor.FromRgb(256, 0, 0));
    }
}

public class PdfTopLeftMigrationTests
{
    [Fact]
    public void DrawTextFromTop_ShouldMatchManualBaselineConversion()
    {
        var fromTop = new PdfDocument();
        var fromTopPage = fromTop.AddPage();
        fromTopPage.DrawTextFromTop("syncfusion", 40, 40, 12, PdfFontFamily.Helvetica);

        var manual = new PdfDocument();
        var manualPage = manual.AddPage();
        manualPage.DrawText("syncfusion", 40, manualPage.Height - 40 - 12, 12, PdfFontFamily.Helvetica);

        var fromTopHash = Convert.ToHexStringLower(SHA256.HashData(fromTop.ToBytes(new PdfSaveOptions { CompressContentStreams = false })));
        var manualHash = Convert.ToHexStringLower(SHA256.HashData(manual.ToBytes(new PdfSaveOptions { CompressContentStreams = false })));

        Assert.Equal(manualHash, fromTopHash);
    }

    [Fact]
    public void DrawParagraphFromTop_ShouldMatchManualBaselineConversion()
    {
        var options = new PdfParagraphOptions
        {
            FontSize = 12,
            FontFamily = PdfFontFamily.Helvetica,
            FillColor = PdfColor.BlueColor
        };

        var fromTop = new PdfDocument();
        var fromTopPage = fromTop.AddPage();
        var fromTopResult = fromTopPage.DrawParagraphFromTop("hello paragraph", 40, 40, 200, options);

        var manual = new PdfDocument();
        var manualPage = manual.AddPage();
        var manualResult = manualPage.DrawParagraph("hello paragraph", 40, manualPage.Height - 40 - 12, 200, options);

        Assert.Equal(manualResult.TopY, fromTopResult.TopY);
        Assert.Equal(manualResult.BottomY, fromTopResult.BottomY);
        Assert.Equal(manualResult.LineCount, fromTopResult.LineCount);
    }

    [Fact]
    public void DrawTextFromTop_ShouldThrow_WhenTopYIsNegative()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        Assert.Throws<ArgumentOutOfRangeException>(() => page.DrawTextFromTop("invalid", 40, -1));
    }

    [Fact]
    public void DrawTextBoxFromTop_ShouldTopAlignTextByDefault()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var result = page.DrawTextBoxFromTop("boxed", 40, 40, 200, 80);

        Assert.Equal(page.Height - 40 - 12, result.TopY);
        Assert.Equal(1, result.LineCount);
    }

    [Fact]
    public void DrawTextBoxFromTop_ShouldMiddleAlignText()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var options = new PdfTextBoxOptions
        {
            FontSize = 12,
            VerticalAlignment = PdfVerticalAlignment.Middle
        };

        var result = page.DrawTextBoxFromTop("boxed", 40, 40, 200, 80, options);
        var expectedTopY = page.Height - 40 - 80 + ((80 - 12) / 2) + 12;

        Assert.Equal(expectedTopY, result.TopY);
    }

    [Fact]
    public void DrawTextBoxFromTop_ShouldBottomAlignText()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var options = new PdfTextBoxOptions
        {
            FontSize = 12,
            VerticalAlignment = PdfVerticalAlignment.Bottom
        };

        var result = page.DrawTextBoxFromTop("boxed", 40, 40, 200, 80, options);
        var expectedTopY = page.Height - 40 - 80 + 12;

        Assert.Equal(expectedTopY, result.TopY);
    }

    [Fact]
    public void DrawTextBoxFromTop_ShouldThrow_WhenHeightIsInvalid()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        Assert.Throws<ArgumentOutOfRangeException>(() => page.DrawTextBoxFromTop("invalid", 40, 40, 200, 0));
    }

    [Fact]
    public void DrawLineFromTop_ShouldMatchManualCoordinateConversion()
    {
        var fromTop = new PdfDocument();
        var fromTopPage = fromTop.AddPage();
        fromTopPage.DrawLineFromTop(40, 40, 120, 60, 2, PdfColor.RedColor);

        var manual = new PdfDocument();
        var manualPage = manual.AddPage();
        manualPage.DrawLine(40, manualPage.Height - 40, 120, manualPage.Height - 60, 2, PdfColor.RedColor);

        Assert.Equal(HashPdf(manual), HashPdf(fromTop));
    }

    [Fact]
    public void DrawRectangleFromTop_ShouldMatchManualCoordinateConversion()
    {
        var fromTop = new PdfDocument();
        var fromTopPage = fromTop.AddPage();
        fromTopPage.DrawRectangleFromTop(40, 40, 120, 60, 2, fill: true, fillColor: PdfColor.BlueColor);

        var manual = new PdfDocument();
        var manualPage = manual.AddPage();
        manualPage.DrawRectangle(40, manualPage.Height - 40 - 60, 120, 60, 2, fill: true, fillColor: PdfColor.BlueColor);

        Assert.Equal(HashPdf(manual), HashPdf(fromTop));
    }

    [Fact]
    public void DrawImageFromTop_ShouldMatchManualCoordinateConversion()
    {
        var imagePath = CreateOnePixelPng();

        try
        {
            var fromTop = new PdfDocument();
            var fromTopPage = fromTop.AddPage();
            fromTopPage.DrawImageFromTop(imagePath, 40, 40, 20, 20);

            var manual = new PdfDocument();
            var manualPage = manual.AddPage();
            manualPage.DrawImage(imagePath, 40, manualPage.Height - 40 - 20, 20, 20);

            Assert.Equal(HashPdf(manual), HashPdf(fromTop));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void DrawImageFromTop_WithoutSize_ShouldMatchManualInferredSizeConversion()
    {
        var imagePath = CreateOnePixelPng();

        try
        {
            var fromTop = new PdfDocument();
            var fromTopPage = fromTop.AddPage();
            fromTopPage.DrawImageFromTop(imagePath, 40, 40);

            var manual = new PdfDocument();
            var manualPage = manual.AddPage();
            manualPage.DrawImage(imagePath, 40, manualPage.Height - 40 - 1);

            Assert.Equal(HashPdf(manual), HashPdf(fromTop));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void DrawImageFromTop_WithBytes_ShouldMatchPathBasedImage()
    {
        var imageBytes = CreateOnePixelPngBytes();

        var fromBytes = new PdfDocument();
        var fromBytesPage = fromBytes.AddPage();
        fromBytesPage.DrawImageFromTop(imageBytes, 40, 40, 20, 20);

        var imagePath = CreateOnePixelPng();

        try
        {
            var fromPath = new PdfDocument();
            var fromPathPage = fromPath.AddPage();
            fromPathPage.DrawImageFromTop(imagePath, 40, 40, 20, 20);

            Assert.Equal(HashPdf(fromPath), HashPdf(fromBytes));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void DrawImageFromTop_WithStream_ShouldMatchByteBasedImage()
    {
        var imageBytes = CreateOnePixelPngBytes();

        var fromStream = new PdfDocument();
        var fromStreamPage = fromStream.AddPage();
        using var stream = new MemoryStream(imageBytes);
        fromStreamPage.DrawImageFromTop(stream, 40, 40, 20, 20);

        var fromBytes = new PdfDocument();
        var fromBytesPage = fromBytes.AddPage();
        fromBytesPage.DrawImageFromTop(imageBytes, 40, 40, 20, 20);

        Assert.Equal(HashPdf(fromBytes), HashPdf(fromStream));
    }

    private static string HashPdf(PdfDocument document)
    {
        return Convert.ToHexStringLower(SHA256.HashData(document.ToBytes(new PdfSaveOptions { CompressContentStreams = false })));
    }

    private static string CreateOnePixelPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"canvas-one-pixel-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, CreateOnePixelPngBytes());
        return path;
    }

    private static byte[] CreateOnePixelPngBytes()
    {
        return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADUlEQVR42mP8z8BQDwAFgwJ/l4p6GAAAAABJRU5ErkJggg==");
    }
}

public class PdfFacadeTests
{
    [Fact]
    public void GenerateToFile_ShouldWritePdfFile()
    {
        var sut = new PdfFacade();
        var document = new PdfDocument();
        document.AddPage().DrawText("facade", 100, 700, 12);
        var path = Path.Combine(Path.GetTempPath(), $"canvas-facade-{Guid.NewGuid():N}.pdf");

        try
        {
            sut.GenerateToFile(document, path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void GetPagesWithText_ShouldReturnFirstPageAfterTextIsDrawn()
    {
        var sut = new PdfFacade();
        var document = new PdfDocument();
        document.AddPage().DrawText("query", 100, 700, 12);

        var pages = sut.GetPagesWithText(document);

        Assert.Contains(1, pages);
    }
}
