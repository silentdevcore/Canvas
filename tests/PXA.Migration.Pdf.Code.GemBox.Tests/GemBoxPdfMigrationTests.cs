using PXA.Migration.Abstractions;
using PXA.Migration.Pdf.Code.GemBox;

namespace PXA.Migration.Pdf.Code.GemBox.Tests;

public sealed class GemBoxPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertBasicDocumentPageTextAndSave()
    {
        var source = """
            using GemBox.Pdf;
            using GemBox.Pdf.Content;

            ComponentInfo.SetLicense("FREE-LIMITED-KEY");
            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Content.DrawText("Hello", new PdfPoint(40, 40));
            doc.Save(outputPath);
            """;
        var sut = new GemBoxPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("GemBox.Pdf", result.MigratedCode);
        Assert.DoesNotContain("ComponentInfo.SetLicense", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGGEMBOX000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGGEMBOX001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGGEMBOX002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGGEMBOX003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGGEMBOX007");
    }

    [Fact]
    public void Migrate_ShouldConvertDrawTextWithCoordinateArguments()
    {
        var source = """
            using GemBox.Pdf;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Content.DrawText("Invoice", 72, 96);
            doc.Save(path);
            """;
        var sut = new GemBoxPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawTextFromTop(\"Invoice\", 72, 96, 12);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGGEMBOX003"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Migrate_ShouldWarnForComplexTextContent()
    {
        var source = """
            using GemBox.Pdf;
            using GemBox.Pdf.Content;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            var text = new PdfFormattedText();
            text.Append("Hello");
            page.Content.DrawText(text, new PdfPoint(40, 40));
            doc.Save(path);
            """;
        var sut = new GemBoxPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.Content.DrawText(text", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGGEMBOX003"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldConvertLinesAndRectangles()
    {
        var source = """
            using GemBox.Pdf;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Content.DrawLine(pen, 40, 700, 555, 700);
            page.Content.DrawRectangle(pen, 40, 620, 200, 80);
            doc.Save(path);
            """;
        var sut = new GemBoxPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLineFromTop(40, 700, 555, 700);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(40, 620, 200, 80);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGGEMBOX004"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Migrate_ShouldConvertDrawLineWithPointArguments()
    {
        var source = """
            using GemBox.Pdf;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Content.DrawLine(pen, new PdfPoint(40, 700), new PdfPoint(555, 700));
            doc.Save(path);
            """;
        var sut = new GemBoxPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLineFromTop(40, 700, 555, 700);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldWarnForImageAndPathContent()
    {
        var source = """
            using GemBox.Pdf;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Content.DrawImage(image, new PdfPoint(40, 120));
            page.Content.DrawPath(path, 40, 620, 200, 80);
            doc.Save(path);
            """;
        var sut = new GemBoxPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGGEMBOX005"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGGEMBOX006"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForFormsSecuritySignaturesAttachmentsAndTaggedPdf()
    {
        var source = """
            using GemBox.Pdf;
            using GemBox.Pdf.Forms;

            var doc = new PdfDocument();
            PdfInteractiveForm form = doc.Form;
            PdfSignature signature = new PdfSignature();
            PdfEncryption encryption = doc.Security.Encryption;
            PdfAttachment attachment = new PdfAttachment();
            PdfTaggedContent tagged = doc.TaggedContent;
            """;
        var sut = new GemBoxPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGGEMBOX020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForExistingPdfEditing()
    {
        var source = """
            using GemBox.Pdf;

            var doc = new PdfDocument();
            doc.Load(inputPath);
            doc.Pages.Clear();
            doc.Pages.ImportPages(otherDocument, 0, 1);
            """;
        var sut = new GemBoxPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGGEMBOX021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
