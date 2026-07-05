using PXA.Migration.Abstractions;

namespace PXA.Migration.DevExpressPdf.Tests;

public sealed class DevExpressPdfMigrationTests
{
    [Fact]
    public void Migrate_BasicGenerationWorkflow_UsesPxaMigrationResult()
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

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP008");
    }

    [Fact]
    public void Migrate_MapsCanvasDiagnosticsToPxaDiagnostics()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            processor.RenderNewPage(PdfPaperSize.Tabloid, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP026" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
