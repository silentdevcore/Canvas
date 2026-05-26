using Canvas.Migration.Abstractions;
using Canvas.Migration.SpirePdf;

namespace Canvas.Migration.SpirePdf.Tests;

public sealed class SpirePdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertBasicDocumentPageTextAndSave()
    {
        var source = """
            using Spire.Pdf;
            using Spire.Pdf.Graphics;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.DrawString("Hello", new PdfFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 40, 40);
            doc.SaveToFile(outputPath);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("Spire.Pdf", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSPIRE001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSPIRE002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSPIRE003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSPIRE007");
    }

    [Fact]
    public void Migrate_ShouldConvertDrawStringWithPointF()
    {
        var source = """
            using Spire.Pdf;
            using Spire.Pdf.Graphics;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.DrawString("Invoice", new PdfFont(PdfFontFamily.Helvetica, 18), PdfBrushes.Black, new PointF(72, 96));
            doc.SaveToFile(path);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawTextFromTop(\"Invoice\", 72, 96, 18);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldConvertLinesAndRectangles()
    {
        var source = """
            using Spire.Pdf;
            using Spire.Pdf.Graphics;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.DrawLine(pen, 40, 700, 555, 700);
            page.Canvas.DrawRectangle(pen, 40, 620, 200, 80);
            page.Canvas.DrawRectangle(pen, new RectangleF(40, 500, 200, 40));
            doc.SaveToFile(path);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLineFromTop(40, 700, 555, 700);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(40, 620, 200, 80);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(40, 500, 200, 40);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSPIRE006");
    }

    [Fact]
    public void Migrate_ShouldConvertFillRectangle()
    {
        var source = """
            using Spire.Pdf;
            using Spire.Pdf.Graphics;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.FillRectangle(brush, 40, 300, 200, 80);
            doc.SaveToFile(path);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawRectangleFromTop(40, 300, 200, 80);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGSPIRE009"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Migrate_ShouldConvertFillRectangleWithRectangleF()
    {
        var source = """
            using Spire.Pdf;
            using Spire.Pdf.Graphics;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.FillRectangle(brush, new RectangleF(40, 300, 200, 80));
            doc.SaveToFile(path);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawRectangleFromTop(40, 300, 200, 80);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldWarnForEllipses()
    {
        var source = """
            using Spire.Pdf;
            using Spire.Pdf.Graphics;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.DrawEllipse(pen, 100, 100, 150, 80);
            page.Canvas.FillEllipse(brush, 100, 200, 150, 80);
            doc.SaveToFile(path);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGSPIRE008"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForImages()
    {
        var source = """
            using Spire.Pdf;

            var doc = new PdfDocument();
            var page = doc.Pages.Add();
            page.Canvas.DrawImage(image, 40, 120, 200, 80);
            doc.SaveToFile(path);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("DrawImage", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGSPIRE005"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForTablesFormsSecurityAndExtraction()
    {
        var source = """
            using Spire.Pdf;
            using Spire.Pdf.Tables;

            var table = new PdfTable();
            var form = new PdfFormWidget();
            var security = new PdfSecurity();
            var extractor = new PdfTextExtractor(page);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGSPIRE020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForExistingPdfEditingAndConversion()
    {
        var source = """
            using Spire.Pdf;

            var doc = new PdfDocument();
            doc.LoadFromFile(inputPath);
            doc.DeletePage(1);
            doc.MergeFiles(files);
            """;
        var sut = new SpirePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGSPIRE021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
