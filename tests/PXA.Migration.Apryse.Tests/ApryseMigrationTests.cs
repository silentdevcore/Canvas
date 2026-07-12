using PXA.Migration.Abstractions;
using PXA.Migration.Apryse;

namespace PXA.Migration.Apryse.Tests;

public sealed class ApryseMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertBasicDocumentPageAndSave()
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

        var result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("pdftron", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.DoesNotContain("PDFNet.Initialize", result.MigratedCode);
        Assert.DoesNotContain("PageCreate", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE000");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE002");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE003");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE004");
    }

    [Fact]
    public void Migrate_ShouldRemovePdfNetInitialize()
    {
        var source = """
            using pdftron;

            PDFNet.Initialize("license-key");
            var doc = new PDFDoc();
            """;
        var sut = new ApryseMigration();

        var result = sut.Migrate(source);

        Assert.DoesNotContain("PDFNet.Initialize", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGAPRYSE000" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Migrate_ShouldConvertPageCreateAndPushBack()
    {
        var source = """
            using pdftron.PDF;

            var doc = new PDFDoc();
            var page1 = doc.PageCreate(new Rect(0, 0, 612, 792));
            doc.PagePushBack(page1);
            """;
        var sut = new ApryseMigration();

        var result = sut.Migrate(source);

        Assert.DoesNotContain("PageCreate", result.MigratedCode);
        Assert.Contains("var page1 = document.AddPage();", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE002");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE003");
    }

    [Fact]
    public void Migrate_ShouldConvertSaveAndRemoveSaveFlags()
    {
        var source = """
            using pdftron.PDF;
            using pdftron.SDF;

            var doc = new PDFDoc();
            doc.Save(outputPath, SDFDoc.SaveOptions.e_linearized);
            """;
        var sut = new ApryseMigration();

        var result = sut.Migrate(source);

        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.DoesNotContain("e_linearized", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGAPRYSE004");
    }

    [Fact]
    public void Migrate_ShouldRemoveAprysePdfUsings()
    {
        var source = """
            using pdftron;
            using pdftron.PDF;
            using pdftron.SDF;
            using System;

            var doc = new PDFDoc();
            """;
        var sut = new ApryseMigration();

        var result = sut.Migrate(source);

        Assert.DoesNotContain("using pdftron", result.MigratedCode);
        Assert.Contains("using System;", result.MigratedCode);
        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
    }
}
