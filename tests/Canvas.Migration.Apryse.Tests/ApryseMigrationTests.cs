using Canvas.Migration.Abstractions;
using Canvas.Migration.Apryse;

namespace Canvas.Migration.Apryse.Tests;

public sealed class ApryseMigrationTests
{
    [Fact]
    public void Migrate_ShouldReportBasicDocumentPageAndSaveWorkflow()
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

        Assert.Contains("// Canvas.Pdf migration report: Apryse SDK", result.MigratedCode);
        Assert.Contains("PDFNet.Initialize(...) detected", result.MigratedCode);
        Assert.Contains("new PDFDoc(...) detected", result.MigratedCode);
        Assert.Contains("PageCreate(...) detected", result.MigratedCode);
        Assert.Contains("PagePushBack(page) detected", result.MigratedCode);
        Assert.Contains("doc.Save(...) detected", result.MigratedCode);
        Assert.Contains("using var doc = new PDFDoc();", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE004");
    }

    [Fact]
    public void Migrate_ShouldReportElementBuilderAndWriterTextWorkflow()
    {
        var source = """
            using pdftron.PDF;

            var builder = new ElementBuilder();
            var writer = new ElementWriter();
            writer.Begin(page);
            writer.WriteElement(builder.CreateTextBegin(font, 12));
            writer.WriteElement(builder.CreateTextRun("Hello"));
            writer.WriteElement(builder.CreateTextEnd());
            """;
        var sut = new ApryseMigration();

        var result = sut.Migrate(source);

        Assert.Contains("ElementBuilder detected", result.MigratedCode);
        Assert.Contains("ElementWriter detected", result.MigratedCode);
        Assert.Contains("ElementWriter.Begin(page) detected", result.MigratedCode);
        Assert.Contains("WriteElement(...) detected", result.MigratedCode);
        Assert.Contains("CreateTextRun(...) detected", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE006");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE007");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE008");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE009");
    }

    [Fact]
    public void Migrate_ShouldReportImageAndShapeElementCandidates()
    {
        var source = """
            using pdftron.PDF;

            var image = builder.CreateImageFromFile(doc, imagePath);
            var rect = builder.CreateRect(40, 40, 200, 80);
            var path = builder.CreatePath(points);
            """;
        var sut = new ApryseMigration();

        var result = sut.Migrate(source);

        Assert.Contains("CreateImageFromFile(...) detected", result.MigratedCode);
        Assert.Contains("CreateRect(...) detected", result.MigratedCode);
        Assert.Contains("CreatePath(...) detected", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE010");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGAPRYSE011");
    }

    [Fact]
    public void Migrate_ShouldWarnForSdfReaderAnnotationsAndFields()
    {
        var source = """
            using pdftron.PDF;
            using pdftron.SDF;

            var sdf = doc.GetSDFDoc();
            var reader = new ElementReader();
            var field = new Field();
            var annot = new Annot();
            """;
        var sut = new ApryseMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Existing-PDF editing, SDF object manipulation, forms, annotations, redaction, OCR/conversion, or signatures require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGAPRYSE020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGAPRYSE021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForConversionOcrAndDigitalSignatureApis()
    {
        var source = """
            using pdftron.PDF;

            Convert.ToPdf(doc, inputPath);
            OCRModule.ImageToPDF(doc, imagePath);
            var signature = new DigitalSignatureField(field);
            """;
        var sut = new ApryseMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Existing-PDF editing, SDF object manipulation, forms, annotations, redaction, OCR/conversion, or signatures require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == MigrationDiagnosticSeverity.Warning
            && diagnostic.Id is "CANMIGAPRYSE020" or "CANMIGAPRYSE021");
    }
}
