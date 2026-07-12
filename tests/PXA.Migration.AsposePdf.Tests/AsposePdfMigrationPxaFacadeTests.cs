using PXA.Migration.Abstractions;

namespace PXA.Migration.AsposePdf.Tests;

public sealed class AsposePdfMigrationPxaFacadeTests
{
    [Fact]
    public void Migrate_BasicDocumentTextAndSave_UsesPxaMigrationResult()
    {
        var source = """
            using Aspose.Pdf;
            using Aspose.Pdf.Text;

            var document = new Document();
            var page = document.Pages.Add();
            var text = new TextFragment("Hello");
            page.Paragraphs.Add(text);
            document.Save(path);
            """;
        var sut = new AsposePdfMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGASPOSE001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGASPOSE008");
    }

    [Fact]
    public void Migrate_MapsPxaWarningsToPxaDiagnostics()
    {
        var source = """
            using Aspose.Pdf;

            var document = new Document();
            var table = new Table();
            document.Save(path);
            """;
        var sut = new AsposePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGASPOSE020" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
