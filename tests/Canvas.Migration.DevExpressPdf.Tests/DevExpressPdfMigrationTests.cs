using Canvas.Migration.Abstractions;
using Canvas.Migration.DevExpressPdf;

namespace Canvas.Migration.DevExpressPdf.Tests;

public sealed class DevExpressPdfMigrationTests
{
    [Fact]
    public void Migrate_BasicGenerationWorkflow_ProducesCanvasCode()
    {
        var source = """
            using DevExpress.Pdf;
            using DevExpress.Drawing;

            using var processor = new PdfDocumentProcessor();
            processor.CreateEmptyDocument();
            using var graphics = processor.CreateGraphics();
            graphics.DrawString("Hello", new DXFont("Arial", 12), DXBrushes.Black, 40, 40);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.DoesNotContain("using DevExpress", result.MigratedCode);
        Assert.DoesNotContain("PdfDocumentProcessor", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP002");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP003");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP004");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP005");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP008");
    }

    [Fact]
    public void Migrate_LineAndRectangleDrawing_ProducesCanvasDrawCalls()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            processor.CreateEmptyDocument();
            using var graphics = processor.CreateGraphics();
            graphics.DrawLine(pen, 40, 700, 555, 700);
            graphics.DrawRectangle(pen, 40, 620, 200, 80);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(outputPath);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLine(40, 700, 555, 700);", result.MigratedCode);
        Assert.Contains("page.DrawRectangle(40, 620, 200, 80);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP006");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP007");
    }

    [Fact]
    public void Migrate_DrawCallsRepositionedAfterAddPage()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            processor.CreateEmptyDocument();
            using var graphics = processor.CreateGraphics();
            graphics.DrawString("Title", new DXFont("Arial", 18), DXBrushes.Black, 40, 750);
            graphics.DrawString("Body", new DXFont("Arial", 12), DXBrushes.Black, 40, 700);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        var addPageIndex = result.MigratedCode.IndexOf("document.AddPage()", StringComparison.Ordinal);
        var drawTitleIndex = result.MigratedCode.IndexOf("DrawTextFromTop(\"Title\"", StringComparison.Ordinal);
        var drawBodyIndex = result.MigratedCode.IndexOf("DrawTextFromTop(\"Body\"", StringComparison.Ordinal);

        Assert.True(addPageIndex >= 0, "AddPage() not found");
        Assert.True(drawTitleIndex > addPageIndex, "Title draw call should come after AddPage");
        Assert.True(drawBodyIndex > addPageIndex, "Body draw call should come after AddPage");
        Assert.Contains("page.DrawTextFromTop(\"Title\", 40, 750, 18);", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Body\", 40, 700, 12);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ExistingPdfProcessing_EmitsWarning()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            processor.LoadDocument(inputPath);
            processor.DeletePage(1);
            processor.SaveDocument(outputPath);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP021" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_FormsAndSignatures_EmitsWarning()
    {
        var source = """
            using DevExpress.Pdf;

            var signer = new PdfDocumentSigner(stream);
            var field = new PdfFormField();
            var options = new PdfEncryptionOptions();
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP022" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ReportExportWorkflow_EmitsWarning()
    {
        var source = """
            using DevExpress.XtraReports.UI;

            var report = new XtraReport();
            report.ExportToPdf(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP020" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
