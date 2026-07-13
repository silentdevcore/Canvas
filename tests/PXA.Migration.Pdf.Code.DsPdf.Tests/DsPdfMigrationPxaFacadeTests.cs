using PXA.Migration.Abstractions;

namespace PXA.Migration.Pdf.Code.DsPdf.Tests;

public sealed class DsPdfMigrationPxaFacadeTests
{
    [Fact]
    public void Migrate_BasicDocumentPageTextAndSave_UsesPxaMigrationResult()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Drawing;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawString("Hello", new TextFormat(), new PointF(40, 40));
            doc.Save(outputPath);
            """;
        var sut = new DsPdfMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\",", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDSPDF001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDSPDF007");
    }

    [Fact]
    public void Migrate_MapsPxaWarningsToPxaDiagnostics()
    {
        var source = """
            using GrapeCity.Documents.Pdf;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawImage(image, new RectangleF(40, 120, 200, 80));
            doc.Save(path);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDSPDF005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
