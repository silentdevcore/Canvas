using Canvas.Migration.Abstractions;
using Canvas.Migration.ActivePdf;

namespace Canvas.Migration.ActivePdf.Tests;

public sealed class ActivePdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertLikelyToolkitDocumentPageTextAndSave()
    {
        var source = """
            using activePDF.Toolkit;

            var toolkit = new Toolkit();
            var page = toolkit.AddPage();
            toolkit.PrintText("Hello", 40, 40);
            toolkit.Save(outputPath);
            """;
        var sut = new ActivePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using activePDF", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGACTIVE000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGACTIVE001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGACTIVE002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGACTIVE003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGACTIVE007");
    }

    [Fact]
    public void Migrate_ShouldConvertBeginPageAndDrawText()
    {
        var source = """
            using ActivePDF.Toolkit;

            var doc = new APDoc();
            var activePage = doc.BeginPage();
            doc.DrawText("Invoice", 72, 96);
            doc.CloseDocument(path);
            """;
        var sut = new ActivePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var activePage = document.AddPage();", result.MigratedCode);
        Assert.Contains("activePage.DrawTextFromTop(\"Invoice\", 72, 96, 12);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldConvertLinesAndRectangles()
    {
        var source = """
            using activePDF.Toolkit;

            var toolkit = new Toolkit();
            var page = toolkit.AddPage();
            toolkit.DrawLine(40, 700, 555, 700);
            toolkit.DrawRectangle(40, 620, 200, 80);
            toolkit.SaveAs(path);
            """;
        var sut = new ActivePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLineFromTop(40, 700, 555, 700);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(40, 620, 200, 80);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGACTIVE006");
    }

    [Fact]
    public void Migrate_ShouldWarnForImageAndStampDrawing()
    {
        var source = """
            using activePDF.Toolkit;

            var toolkit = new Toolkit();
            var page = toolkit.AddPage();
            toolkit.StampImage(image, 40, 120, 200, 80);
            toolkit.Save(path);
            """;
        var sut = new ActivePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("StampImage", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGACTIVE005"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForProductFamiliesComPrinterAndSecurity()
    {
        var source = """
            using activePDF.DocConverter;
            using activePDF.WebGrabber;

            var converter = new DocConverter();
            var grabber = new WebGrabber();
            var printer = new Printer();
            var signature = new Signature();
            var security = new Security();
            var stamp = new Stamp();
            """;
        var sut = new ActivePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGACTIVE020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForHtmlConversionMergePrintAndExistingPdfEditing()
    {
        var source = """
            using activePDF.DocConverter;

            var converter = new DocConverter();
            converter.Open(inputPath);
            converter.ConvertToPDF(url, outputPath);
            converter.Merge(other);
            converter.Print(printerName);
            """;
        var sut = new ActivePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGACTIVE021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
