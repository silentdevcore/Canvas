using Canvas.WebApi.Services;

namespace Canvas.Api.Tests;

public sealed class MigrationServiceTests
{
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
