using PXA.Migration.Abstractions;

namespace PXA.Migration.SpirePdf.Tests;

public sealed class SpirePdfMigrationTests
{
    [Fact]
    public void Migrate_BasicDocumentPageTextAndSave_UsesPxaMigrationResult()
    {
        var source = """
            using Spire.Pdf;
            using Spire.Pdf.Graphics;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.DrawString("Hello", new PdfFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 40, 40);
            doc.SaveToFile(outputPath);
            """;
        var sut = new SpirePdfMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("Spire.Pdf", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGSPIRE001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGSPIRE007");
    }

    [Fact]
    public void Migrate_MapsCanvasWarningsToPxaDiagnostics()
    {
        var source = """
            using Spire.Pdf;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.DrawImage(image, 40, 120, 200, 80);
            doc.SaveToFile(path);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGSPIRE005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
