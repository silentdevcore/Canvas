using PXA.Migration.Abstractions;

namespace PXA.Migration.Pdf.Code.Foxit.Tests;

public sealed class FoxitPdfMigrationPxaFacadeTests
{
    [Fact]
    public void Migrate_BasicDocumentPageAndSave_UsesPxaMigrationResult()
    {
        var source = """
            using foxit;
            using foxit.pdf;

            Library.Initialize(licenseKey);
            using var doc = new PDFDoc();
            var page = doc.InsertPage(0, PageSize.e_SizeA4);
            doc.SaveAs(outputPath);
            """;
        var sut = new FoxitPdfMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using foxit", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT000");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT007");
    }

    [Fact]
    public void Migrate_MapsPxaWarningsToPxaDiagnostics()
    {
        var source = """
            using foxit.pdf;

            var doc = new PDFDoc();
            var page = doc.InsertPage(0, PageSize.e_SizeA4);
            var graphics = page.GetGraphics();
            graphics.DrawImage(image, 40, 120, 200, 80);
            doc.SaveAs(path);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGFOXIT005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
