using PXA.Migration.Abstractions;

namespace PXA.Migration.GemBoxPdf.Tests;

public sealed class GemBoxPdfMigrationTests
{
    [Fact]
    public void Migrate_BasicDocumentPageTextAndSave_UsesPxaMigrationResult()
    {
        var source = """
            using GemBox.Pdf;
            using GemBox.Pdf.Content;

            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Content.DrawText("Hello", new PdfPoint(40, 40));
            doc.Save(outputPath);
            """;
        var sut = new GemBoxPdfMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("GemBox.Pdf", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGGEMBOX000");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGGEMBOX007");
    }

    [Fact]
    public void Migrate_MapsCanvasWarningsToPxaDiagnostics()
    {
        var source = """
            using GemBox.Pdf;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Content.DrawImage(image, new PdfPoint(40, 120));
            page.Content.DrawPath(path, 40, 620, 200, 80);
            doc.Save(path);
            """;
        var sut = new GemBoxPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGGEMBOX005" && d.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGGEMBOX006" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
