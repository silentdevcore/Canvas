using PXA.Migration.Abstractions;

namespace PXA.Migration.Apryse.Tests;

public sealed class ApryseMigrationPxaFacadeTests
{
    [Fact]
    public void Migrate_BasicDocumentPageAndSave_UsesPxaMigrationResult()
    {
        var source = """
            using pdftron;
            using pdftron.PDF;
            using pdftron.SDF;

            PDFNet.Initialize();
            using var doc = new PDFDoc();
            var page = doc.PageCreate();
            doc.PagePushBack(page);
            doc.Save(path, SDFDoc.SaveOptions.e_linearized);
            """;
        var sut = new ApryseMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("pdftron", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE000");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE004");
    }

    [Fact]
    public void Migrate_MapsCanvasInfoDiagnosticsToPxaDiagnostics()
    {
        var source = """
            using pdftron;

            PDFNet.Initialize("license-key");
            var doc = new PDFDoc();
            """;
        var sut = new ApryseMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGAPRYSE000" && d.Severity == MigrationDiagnosticSeverity.Info);
    }
}
