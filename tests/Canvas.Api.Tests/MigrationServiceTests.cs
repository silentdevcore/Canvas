using Canvas.WebApi.Services;

namespace Canvas.Api.Tests;

public sealed class MigrationServiceTests
{
    [Fact]
    public void Convert_ShouldUseAsposeRoslynPilotAndReturnSummary()
    {
        var source = """
            using Aspose.Pdf;
            using Aspose.Pdf.Text;

            var document = new Document();
            var page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Smoke"));
            document.Save(outputStream);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("Aspose", source);

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("page.DrawTextFromTop(\"Smoke\", 40, 40, 12);", result.CanvasCode);
        Assert.Contains("document.Save(outputStream);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGASPOSE003");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.WarningCount);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseIText7RoslynPilotAndReturnSummary()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Layout;
            using iText.Layout.Element;
            using iText.Layout.Properties;

            using var writer = new PdfWriter(outputStream);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            document.ShowTextAligned(new Paragraph("Smoke"), 72, 700, TextAlignment.LEFT);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("iText7", source);

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.Contains("page.DrawText(\"Smoke\", 72, 700, 12);", result.CanvasCode);
        Assert.Contains("document.Save(outputStream);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGITEXT009");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.WarningCount);
        Assert.Equal(0, result.Summary.ErrorCount);
    }
}
