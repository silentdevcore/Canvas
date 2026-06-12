using Canvas.Migration.Abstractions;
using Canvas.Migration.LeadtoolsPdf;

namespace Canvas.Migration.LeadtoolsPdf.Tests;

public sealed class LeadtoolsPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertLikelyDocumentPageTextAndSave()
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

        var result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using Leadtools", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGLEAD000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGLEAD001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGLEAD002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGLEAD003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGLEAD007");
    }

    [Fact]
    public void Migrate_ShouldConvertPagesAddAndDrawString()
    {
        var source = """
            using Leadtools.Pdf;

            var document = new PdfDocument();
            var leadPage = document.Pages.Add();
            leadPage.DrawString("Invoice", 72, 96);
            document.SaveToFile(path);
            """;
        var sut = new LeadtoolsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var leadPage = document.AddPage();", result.MigratedCode);
        Assert.Contains("leadPage.DrawTextFromTop(\"Invoice\", 72, 96, 12);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldConvertLinesAndRectangles()
    {
        var source = """
            using Leadtools.Pdf;

            var doc = new PDFDocument();
            var page = doc.NewPage();
            page.DrawLine(40, 700, 555, 700);
            page.DrawRectangle(40, 620, 200, 80);
            doc.Write(path);
            """;
        var sut = new LeadtoolsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLineFromTop(40, 700, 555, 700);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(40, 620, 200, 80);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGLEAD006");
    }

    [Fact]
    public void Migrate_ShouldWarnForImagesAndRasterDrawing()
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

        Assert.Contains("DrawImage", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGLEAD005"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForOcrRasterBarcodeConversionAndSecurity()
    {
        var source = """
            using Leadtools;
            using Leadtools.Document;

            var codecs = new RasterCodecs();
            var image = new RasterImage();
            var ocr = OcrEngineManager.CreateEngine(OcrEngineType.LEAD, false);
            var converter = new DocumentConverter();
            var factory = new DocumentFactory();
            var barcode = new BarcodeEngine();
            var security = new Security();
            """;
        var sut = new LeadtoolsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGLEAD020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForExistingPdfEditingAndConversion()
    {
        var source = """
            using Leadtools.Pdf;

            var doc = new PDFDocument();
            doc.LoadFromFile(inputPath);
            doc.Convert(outputPath);
            doc.DeletePage(2);
            """;
        var sut = new LeadtoolsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGLEAD021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
