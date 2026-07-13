using PXA.Migration.Abstractions;

namespace PXA.Migration.Pdf.Code.ActivePdf.Tests;

public sealed class ActivePdfMigrationPxaFacadeTests
{
    [Fact]
    public void Migrate_LikelyToolkitDocumentPageTextAndSave_UsesPxaMigrationResult()
    {
        var source = """
            using activePDF.Toolkit;

            var toolkit = new Toolkit();
            var page = toolkit.AddPage();
            toolkit.PrintText("Hello", 40, 40);
            toolkit.Save(outputPath);
            """;
        var sut = new ActivePdfMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using activePDF", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGACTIVE000");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGACTIVE007");
    }

    [Fact]
    public void Migrate_MapsPxaWarningsToPxaDiagnostics()
    {
        var source = """
            using activePDF.Toolkit;

            var toolkit = new Toolkit();
            var page = toolkit.AddPage();
            toolkit.StampImage(image, 40, 120, 200, 80);
            toolkit.Save(path);
            """;
        var sut = new ActivePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGACTIVE005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
