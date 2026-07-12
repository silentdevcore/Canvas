using PXA.Migration.Abstractions;
using PXA.Migration.PdfKitNet;

namespace PXA.Migration.PdfKitNet.Tests;

public sealed class PdfKitNetMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertBasicLikelyDocumentPageTextAndSave()
    {
        var source = """
            using PdfKitNet;

            var doc = new Document();
            var page = doc.NewPage();
            page.DrawText("Hello", 40, 40);
            doc.Render(outputPath);
            """;
        var sut = new PdfKitNetMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using PdfKitNet;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFKIT000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFKIT001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFKIT002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFKIT003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFKIT007");
    }

    [Fact]
    public void Migrate_ShouldConvertPagesAddAndDrawString()
    {
        var source = """
            using PDFKit;

            var document = new PDFDocument();
            var pdfPage = document.Pages.Add();
            pdfPage.DrawString("Invoice", 72, 96);
            document.Save(path);
            """;
        var sut = new PdfKitNetMigration();

        var result = sut.Migrate(source);

        Assert.DoesNotContain("using PDFKit;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var pdfPage = document.AddPage();", result.MigratedCode);
        Assert.Contains("pdfPage.DrawTextFromTop(\"Invoice\", 72, 96, 12);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldConvertLinesAndRectangles()
    {
        var source = """
            using PdfKit;

            var doc = new Document();
            var page = doc.AddPage();
            page.DrawLine(40, 700, 555, 700);
            page.DrawRectangle(40, 620, 200, 80);
            doc.Write(path);
            """;
        var sut = new PdfKitNetMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLineFromTop(40, 700, 555, 700);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(40, 620, 200, 80);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFKIT006");
    }

    [Fact]
    public void Migrate_ShouldWarnForImages()
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

        Assert.Contains("DrawImage", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFKIT005"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForFormsSecuritySignaturesAnnotationsTablesAndTemplates()
    {
        var source = """
            using PdfKitNet;

            var form = new AcroForm();
            var signature = new Signature();
            var security = new Security();
            var annotation = new Annotation();
            var table = new Table();
            var template = new Template();
            """;
        var sut = new PdfKitNetMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFKIT020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForExistingPdfEditing()
    {
        var source = """
            using PdfKitNet;

            var doc = new Document();
            doc.Load(inputPath);
            doc.ImportPage(1);
            doc.Merge(other);
            doc.DeletePage(2);
            """;
        var sut = new PdfKitNetMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFKIT021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
