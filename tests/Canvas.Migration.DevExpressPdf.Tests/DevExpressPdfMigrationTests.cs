using Canvas.Migration.Abstractions;
using Canvas.Migration.DevExpressPdf;

namespace Canvas.Migration.DevExpressPdf.Tests;

public sealed class DevExpressPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldReportGeneratedDocumentDrawingWorkflow()
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

        Assert.Contains("// Canvas.Pdf migration report: DevExpress PDF", result.MigratedCode);
        Assert.Contains("Detected PdfDocumentProcessor", result.MigratedCode);
        Assert.Contains("CreateEmptyDocument(...) detected", result.MigratedCode);
        Assert.Contains("CreateGraphics(...) detected", result.MigratedCode);
        Assert.Contains("DrawString(...) detected for `\"Hello\"`", result.MigratedCode);
        Assert.Contains("RenderNewPage(...) detected", result.MigratedCode);
        Assert.Contains("SaveDocument(...) detected", result.MigratedCode);
        Assert.Contains("graphics.DrawString(\"Hello\"", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDEVEXP001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDEVEXP002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDEVEXP003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDEVEXP004");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDEVEXP005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDEVEXP008");
    }

    [Fact]
    public void Migrate_ShouldReportLineAndRectangleDrawingCandidates()
    {
        var source = """
            using DevExpress.Pdf;

            graphics.DrawLine(pen, 40, 700, 555, 700);
            graphics.DrawRectangle(pen, 40, 620, 200, 80);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("DrawLine(...) detected", result.MigratedCode);
        Assert.Contains("DrawRectangle(...) detected", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDEVEXP006");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDEVEXP007");
    }

    [Fact]
    public void Migrate_ShouldWarnForExistingPdfProcessing()
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

        Assert.Contains("Existing-PDF editing, forms, signatures, encryption, or document operations require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGDEVEXP021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForFormsSignaturesAndEncryption()
    {
        var source = """
            using DevExpress.Pdf;

            var signer = new PdfDocumentSigner(stream);
            var field = new PdfFormField();
            var options = new PdfEncryptionOptions();
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Existing-PDF editing, forms, signatures, encryption, or document operations require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGDEVEXP022"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForReportExportWorkflows()
    {
        var source = """
            using DevExpress.XtraReports.UI;

            var report = new XtraReport();
            report.ExportToPdf(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("DevExpress reporting/export APIs require report template review before Canvas.Pdf rewrite.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGDEVEXP020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
