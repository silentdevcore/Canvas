using Canvas.Migration.Abstractions;
using Canvas.Migration.DsPdf;

namespace Canvas.Migration.DsPdf.Tests;

public sealed class DsPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertBasicDocumentPageTextAndSave()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Drawing;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawString("Hello", new TextFormat(), new PointF(40, 40));
            doc.Save(outputPath);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("GrapeCity.Documents", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\",", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDSPDF001" && d.Severity == MigrationDiagnosticSeverity.Info);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDSPDF002" && d.Severity == MigrationDiagnosticSeverity.Info);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDSPDF003" && d.Severity == MigrationDiagnosticSeverity.Info);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDSPDF007" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Migrate_ShouldExtractFontSizeFromTextFormat()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Drawing;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawString("Invoice", new TextFormat { FontSize = 18 }, new PointF(72, 72));
            doc.Save(path);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawTextFromTop(\"Invoice\", 72, 72, 18);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDSPDF003");
    }

    [Fact]
    public void Migrate_ShouldConvertDrawLineWithFiveArgs()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Drawing;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawLine(pen, 40, 700, 555, 700);
            doc.Save(path);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLineFromTop(40, 700, 555, 700);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDSPDF006" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Migrate_ShouldConvertDrawLineWithPointFArgs()
    {
        var source = """
            using DS.Documents.Pdf;
            using DS.Documents.Drawing;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawLine(pen, new PointF(40, 700), new PointF(555, 700));
            doc.Save(path);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLineFromTop(40, 700, 555, 700);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldConvertDrawRectangleAndFillRectangle()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Drawing;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawRectangle(pen, new RectangleF(40, 620, 200, 60));
            page.Graphics.FillRectangle(brush, new RectangleF(40, 500, 200, 40));
            doc.Save(path);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawRectangleFromTop(40, 620, 200, 60);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(40, 500, 200, 40, 1, true);", result.MigratedCode);
        Assert.DoesNotContain("FillRectangle", result.MigratedCode);
        Assert.DoesNotContain("DrawRectangle(pen", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldWarnForDrawImageAndKeepStatement()
    {
        var source = """
            using GrapeCity.Documents.Pdf;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawImage(image, new RectangleF(40, 120, 200, 80));
            doc.Save(path);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("DrawImage", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDSPDF005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForAdvancedLayoutAndTables()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Layout;

            var table = new TableRenderer();
            var layout = new LayoutHost();
            var text = new TextLayout();
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDSPDF023" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForExistingPdfEditingAndMerge()
    {
        var source = """
            using GrapeCity.Documents.Pdf;

            var doc = new GcPdfDocument();
            doc.Load(inputPath);
            doc.DeletePage(1);
            doc.MergeWithDocument(otherDocument);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDSPDF021" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForFormsComplianceAndSecurity()
    {
        var source = """
            using GrapeCity.Documents.Pdf;

            var doc = new GcPdfDocument();
            doc.SaveAsPdfA(path);
            doc.Sign(signatureProperties);
            doc.ApplyRedactions();
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDSPDF022" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldConvertRealisticInvoice()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Drawing;

            var doc = new GcPdfDocument();
            var page = doc.NewPage();
            page.Graphics.DrawString("Invoice #2024", new TextFormat { FontSize = 18 }, new PointF(72, 72));
            page.Graphics.DrawLine(pen, 72, 100, 540, 100);
            page.Graphics.DrawString("Thank you for your order.", new TextFormat { FontSize = 12 }, new PointF(72, 120));
            page.Graphics.DrawRectangle(pen, new RectangleF(72, 200, 468, 300));
            page.Graphics.FillRectangle(brush, new RectangleF(72, 200, 468, 20));
            doc.Save(outputPath);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Invoice #2024\", 72, 72, 18);", result.MigratedCode);
        Assert.Contains("page.DrawLineFromTop(72, 100, 540, 100);", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Thank you for your order.\", 72, 120, 12);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(72, 200, 468, 300);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(72, 200, 468, 20, 1, true);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
    }
}
