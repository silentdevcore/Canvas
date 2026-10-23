using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Pdf.Code.Leadtools.Tests;

public sealed class LeadtoolsPdfMigrationPvadFacadeTests
{
    [Fact]
    public void Migrate_LikelyDocumentPageTextAndSave_UsesPvadMigrationResult()
    {
        var source = """
            using Leadtools;
            using Leadtools.Pdf;

            var doc = new PDFDocument();
            var page = doc.AddPage();
            page.DrawText("Hello", 40, 40);
            doc.Save(outputPath);
            """;
        var sut = new LeadtoolsPdfMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using PVAD.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using Leadtools", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGLEAD000");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGLEAD007");
    }

    [Fact]
    public void Migrate_MapsPvadWarningsToPvadDiagnostics()
    {
        var source = """
            using Leadtools.Pdf;

            var doc = new PDFDocument();
            var page = doc.AddPage();
            page.DrawImage(image, 40, 120, 200, 80);
            doc.Save(path);
            """;
        var sut = new LeadtoolsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGLEAD005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
