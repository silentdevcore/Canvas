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
    public void Convert_ShouldUseApryseRoslynReportingPilotAndReturnSummary()
    {
        var source = """
            using pdftron.PDF;

            using var doc = new PDFDoc();
            var page = doc.PageCreate();
            doc.PagePushBack(page);
            doc.Save(outputPath, SDFDoc.SaveOptions.e_linearized);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("Apryse", source);

        Assert.Contains("// Canvas.Pdf migration report: Apryse SDK", result.CanvasCode);
        Assert.Contains("new PDFDoc(...) detected", result.CanvasCode);
        Assert.Contains("PagePushBack(page) detected", result.CanvasCode);
        Assert.Contains("doc.Save(outputPath", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGAPRYSE001");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
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

    [Fact]
    public void Convert_ShouldUseIronPdfRoslynReportingPilotAndReturnSummary()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlAsPdf("<h1>Smoke</h1>");
            pdf.SaveAs(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("IronPdf", source);

        Assert.Contains("// Canvas.Pdf migration report: IronPDF", result.CanvasCode);
        Assert.Contains("Literal HTML detected for manual extraction: <h1>Smoke</h1>", result.CanvasCode);
        Assert.Contains("renderer.RenderHtmlAsPdf(\"<h1>Smoke</h1>\");", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGIRONPDF002");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.True(result.Summary.WarningCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseDevExpressRoslynReportingPilotAndReturnSummary()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            processor.CreateEmptyDocument();
            using var graphics = processor.CreateGraphics();
            graphics.DrawString("Smoke", font, brush, 40, 40);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("DevExpress", source);

        Assert.Contains("// Canvas.Pdf migration report: DevExpress PDF", result.CanvasCode);
        Assert.Contains("DrawString(...) detected for `\"Smoke\"`", result.CanvasCode);
        Assert.Contains("processor.SaveDocument(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGDEVEXP005");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseDsPdfRoslynReportingPilotAndReturnSummary()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Drawing;

            var document = new GcPdfDocument();
            var page = document.NewPage();
            page.Graphics.DrawString("Smoke", new TextFormat(), new PointF(40, 40));
            document.Save(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("DsPdf", source);

        Assert.Contains("// Canvas.Pdf migration report: DsPdf / Document Solutions", result.CanvasCode);
        Assert.Contains("new GcPdfDocument(...) detected", result.CanvasCode);
        Assert.Contains("DrawString(...) detected", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGDSPDF003");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseFoxitRoslynReportingPilotAndReturnSummary()
    {
        var source = """
            using foxit.pdf;

            using var doc = new PDFDoc();
            var page = doc.InsertPage(0, PageSize.e_SizeA4);
            graphics.DrawText("Smoke", font, 40, 40);
            doc.SaveAs(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("Foxit", source);

        Assert.Contains("// Canvas.Pdf migration report: Foxit PDF SDK", result.CanvasCode);
        Assert.Contains("new PDFDoc(...) detected", result.CanvasCode);
        Assert.Contains("DrawText(...) detected", result.CanvasCode);
        Assert.Contains("doc.SaveAs(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGFOXIT004");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }
}
