using PXA.Migration.Abstractions;

namespace PXA.Migration.PdfKitNet.Tests;

public sealed class PdfKitNetMigrationPxaFacadeTests
{
    [Fact]
    public void Migrate_BasicLikelyDocumentPageTextAndSave_UsesPxaMigrationResult()
    {
        var source = """
            using PdfKitNet;

            var doc = new Document();
            var page = doc.NewPage();
            page.DrawText("Hello", 40, 40);
            doc.Render(outputPath);
            """;
        var sut = new PdfKitNetMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using PdfKitNet;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGPDFKIT000");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGPDFKIT007");
    }

    [Fact]
    public void Migrate_MapsCanvasWarningsToPxaDiagnostics()
    {
        var source = """
            using PdfKitNet;

            var doc = new Document();
            var page = doc.NewPage();
            page.DrawImage(image, 40, 120, 200, 80);
            doc.Save(path);
            """;
        var sut = new PdfKitNetMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGPDFKIT005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
