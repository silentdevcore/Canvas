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
    public void Convert_ShouldUseApryseRoslynConverterAndReturnSummary()
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

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGAPRYSE001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGAPRYSE003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGAPRYSE004");
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
    public void Convert_ShouldUseIronPdfRoslynConverterAndReturnCanvasScaffold()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlAsPdf("<h1>Smoke</h1>");
            pdf.SaveAs(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("IronPdf", source);

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.DoesNotContain("ChromePdfRenderer", result.CanvasCode);
        Assert.DoesNotContain("RenderHtmlAsPdf", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGIRONPDF001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGIRONPDF002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGIRONPDF006");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.True(result.Summary.WarningCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseDevExpressRoslynConverterAndReturnSummary()
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

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("page.DrawTextFromTop(\"Smoke\", 40, 40, 12);", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGDEVEXP001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGDEVEXP005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGDEVEXP008");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseDsPdfRoslynConverterAndReturnSummary()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Drawing;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawString("Smoke", new TextFormat(), new PointF(40, 40));
            doc.Save(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("DsPdf", source);

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("page.DrawTextFromTop(\"Smoke\",", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGDSPDF001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGDSPDF003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGDSPDF007");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseFoxitRoslynConverterAndReturnSummary()
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

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("page.DrawTextFromTop(\"Smoke\",", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.DoesNotContain("PDFDoc", result.CanvasCode);
        Assert.DoesNotContain("InsertPage", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGFOXIT001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGFOXIT004");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGFOXIT007");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseGemBoxRoslynConverterAndReturnSummary()
    {
        var source = """
            using GemBox.Pdf;
            using GemBox.Pdf.Content;

            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Content.DrawText("Smoke", new PdfPoint(40, 40));
            doc.Save(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("GemBox", source);

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.DoesNotContain("GemBox.Pdf", result.CanvasCode);
        Assert.DoesNotContain("ComponentInfo.SetLicense", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("page.DrawTextFromTop(\"Smoke\", 40, 40, 12);", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGGEMBOX001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGGEMBOX003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGGEMBOX007");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseSpireRoslynConverterAndReturnSummary()
    {
        var source = """
            using Spire.Pdf;
            using Spire.Pdf.Graphics;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.DrawString("Smoke", new PdfFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 40, 40);
            doc.SaveToFile(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("Spire", source);

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.DoesNotContain("Spire.Pdf", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("page.DrawTextFromTop(\"Smoke\", 40, 40, 12);", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGSPIRE001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGSPIRE003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGSPIRE007");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUsePdfKitNetRoslynConverterAndReturnSummary()
    {
        var source = """
            using PdfKitNet;

            var doc = new Document();
            var page = doc.NewPage();
            page.DrawText("Smoke", 40, 40);
            doc.Render(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("PdfKitNet", source);

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("page.DrawTextFromTop(\"Smoke\", 40, 40, 12);", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGPDFKIT000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGPDFKIT003");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.True(result.Summary.WarningCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseLeadtoolsRoslynConverterAndReturnSummary()
    {
        var source = """
            using Leadtools.Pdf;

            var doc = new PDFDocument();
            var page = doc.AddPage();
            page.DrawText("Smoke", 40, 40);
            doc.Save(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("Leadtools", source);

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.DoesNotContain("using Leadtools", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("page.DrawTextFromTop(\"Smoke\", 40, 40, 12);", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGLEAD000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGLEAD003");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.True(result.Summary.WarningCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }

    [Fact]
    public void Convert_ShouldUseActivePdfRoslynConverterAndReturnSummary()
    {
        var source = """
            using activePDF.Toolkit;

            var toolkit = new Toolkit();
            var page = toolkit.AddPage();
            toolkit.PrintText("Smoke", 40, 40);
            toolkit.Save(outputPath);
            """;
        var sut = new MigrationService();

        var result = sut.Convert("ActivePdf", source);

        Assert.Contains("using Canvas.Pdf;", result.CanvasCode);
        Assert.DoesNotContain("using activePDF", result.CanvasCode);
        Assert.Contains("var document = new PdfDocument();", result.CanvasCode);
        Assert.Contains("var page = document.AddPage();", result.CanvasCode);
        Assert.Contains("page.DrawTextFromTop(\"Smoke\", 40, 40, 12);", result.CanvasCode);
        Assert.Contains("document.Save(outputPath);", result.CanvasCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGACTIVE000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CANMIGACTIVE003");
        Assert.Equal(result.Diagnostics.Count, result.Summary.TotalDiagnostics);
        Assert.True(result.Summary.ConvertedCount > 0);
        Assert.True(result.Summary.WarningCount > 0);
        Assert.Equal(0, result.Summary.ErrorCount);
    }
}
