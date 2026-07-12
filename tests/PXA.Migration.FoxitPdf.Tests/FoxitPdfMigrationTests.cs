using PXA.Migration.Abstractions;
using PXA.Migration.FoxitPdf;

namespace PXA.Migration.FoxitPdf.Tests;

public sealed class FoxitPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertBasicDocumentPageAndSave()
    {
        var source = """
            using foxit;
            using foxit.pdf;

            Library.Initialize(licenseKey);
            using var doc = new PDFDoc();
            var page = doc.InsertPage(0, PageSize.e_SizeA4);
            doc.SaveAs(outputPath);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using foxit", result.MigratedCode);
        Assert.DoesNotContain("Library.Initialize", result.MigratedCode);
        Assert.DoesNotContain("PDFDoc", result.MigratedCode);
        Assert.DoesNotContain("InsertPage", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT000" && d.Severity == MigrationDiagnosticSeverity.Info);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT001" && d.Severity == MigrationDiagnosticSeverity.Info);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT002" && d.Severity == MigrationDiagnosticSeverity.Info);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT007" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Migrate_ShouldRemoveGetGraphicsAndConvertDrawCalls()
    {
        var source = """
            using foxit.pdf;

            var doc = new PDFDoc();
            var page = doc.InsertPage(0, PageSize.e_SizeA4);
            var graphics = page.GetGraphics();
            graphics.DrawText("Hello", font, 40, 40);
            graphics.DrawLine(pen, 40, 700, 555, 700);
            graphics.DrawRect(pen, 40, 620, 200, 80);
            page.GenerateContent();
            doc.SaveAs(path);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.DoesNotContain("GetGraphics", result.MigratedCode);
        Assert.DoesNotContain("GenerateContent", result.MigratedCode);
        Assert.DoesNotContain("graphics.", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("page.DrawLineFromTop(40, 700, 555, 700);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(40, 620, 200, 80);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT003");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT004");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT006");
    }

    [Fact]
    public void Migrate_ShouldConvertFillRect()
    {
        var source = """
            using foxit.pdf;

            var doc = new PDFDoc();
            var page = doc.InsertPage(0, PageSize.e_SizeA4);
            var graphics = page.GetGraphics();
            graphics.FillRect(brush, 40, 500, 200, 40);
            doc.SaveAs(path);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawRectangleFromTop(40, 500, 200, 40, 1, true);", result.MigratedCode);
        Assert.DoesNotContain("FillRect", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGFOXIT006");
    }

    [Fact]
    public void Migrate_ShouldWarnForDrawImageAndKeepStatement()
    {
        var source = """
            using foxit.pdf;

            var doc = new PDFDoc();
            var page = doc.InsertPage(0, PageSize.e_SizeA4);
            var graphics = page.GetGraphics();
            graphics.DrawImage(image, 40, 120, 200, 80);
            doc.SaveAs(path);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("DrawImage", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGFOXIT005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForExistingPdfEditing()
    {
        var source = """
            using foxit.pdf;

            var doc = new PDFDoc(inputPath);
            doc.DeletePage(1);
            doc.Save(outputPath);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGFOXIT020" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForFormsAnnotationsAndSecurity()
    {
        var source = """
            using foxit.pdf;

            var form = doc.GetForm();
            var annot = page.GetAnnot(0);
            doc.Sign(signature);
            doc.SetSecurity(securityHandler);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGFOXIT020" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForRenderingOcrAndConversionApis()
    {
        var source = """
            using foxit.pdf;

            PDFViewCtrl view = new PDFViewCtrl();
            renderer.RenderPageToBitmap(page);
            ocr.StartOCR(image);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGFOXIT021" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldConvertRealisticInvoice()
    {
        var source = """
            using foxit;
            using foxit.pdf;

            Library.Initialize(licenseKey);
            var doc = new PDFDoc();
            var page = doc.InsertPage(0, PageSize.e_SizeA4);
            var graphics = page.GetGraphics();
            graphics.DrawText("Invoice #2024", font18, 72, 72);
            graphics.DrawLine(pen, 72, 100, 540, 100);
            graphics.DrawText("Thank you for your order.", font12, 72, 130);
            graphics.DrawRect(pen, 72, 200, 468, 300);
            graphics.FillRect(brush, 72, 200, 468, 20);
            page.GenerateContent();
            doc.SaveAs(outputPath);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Invoice #2024\", 72, 72, 12);", result.MigratedCode);
        Assert.Contains("page.DrawLineFromTop(72, 100, 540, 100);", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Thank you for your order.\", 72, 130, 12);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(72, 200, 468, 300);", result.MigratedCode);
        Assert.Contains("page.DrawRectangleFromTop(72, 200, 468, 20, 1, true);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.DoesNotContain("Library.Initialize", result.MigratedCode);
        Assert.DoesNotContain("GetGraphics", result.MigratedCode);
        Assert.DoesNotContain("GenerateContent", result.MigratedCode);
    }
}
