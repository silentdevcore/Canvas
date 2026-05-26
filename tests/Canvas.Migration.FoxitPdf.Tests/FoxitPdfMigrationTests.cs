using Canvas.Migration.Abstractions;
using Canvas.Migration.FoxitPdf;

namespace Canvas.Migration.FoxitPdf.Tests;

public sealed class FoxitPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldReportBasicDocumentPageAndSaveWorkflow()
    {
        var source = """
            using foxit;
            using foxit.pdf;

            Library.Initialize(licenseKey);
            using var doc = new PDFDoc();
            var page = doc.InsertPage(0, PageSize.e_SizeA4);
            doc.SaveAs(path);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("// Canvas.Pdf migration report: Foxit PDF SDK", result.MigratedCode);
        Assert.Contains("Library.Initialize(...) detected", result.MigratedCode);
        Assert.Contains("new PDFDoc(...) detected", result.MigratedCode);
        Assert.Contains("InsertPage(...) detected", result.MigratedCode);
        Assert.Contains("SaveAs(...) detected", result.MigratedCode);
        Assert.Contains("using var doc = new PDFDoc();", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGFOXIT000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGFOXIT001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGFOXIT002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGFOXIT007");
    }

    [Fact]
    public void Migrate_ShouldReportTextImageAndShapeDrawingCandidates()
    {
        var source = """
            using foxit.pdf;

            graphics.DrawText("Hello", font, 40, 40);
            graphics.DrawImage(image, 40, 120, 200, 80);
            graphics.DrawLine(pen, 40, 700, 555, 700);
            graphics.DrawRect(pen, 40, 620, 200, 80);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("DrawText(...) detected", result.MigratedCode);
        Assert.Contains("DrawImage(...) detected", result.MigratedCode);
        Assert.Contains("DrawLine(...) detected", result.MigratedCode);
        Assert.Contains("DrawRect(...) detected", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGFOXIT004");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGFOXIT005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGFOXIT006");
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

        Assert.Contains("Existing-PDF editing, forms, annotations, security/signing, rendering, viewer, OCR/conversion, or redaction APIs require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGFOXIT001");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGFOXIT020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForFormsAnnotationsSignaturesAndSecurity()
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

        Assert.Contains("Existing-PDF editing, forms, annotations, security/signing, rendering, viewer, OCR/conversion, or redaction APIs require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGFOXIT020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForRenderingOcrViewerAndConversionApis()
    {
        var source = """
            using foxit.pdf;

            PDFViewCtrl view = new PDFViewCtrl();
            renderer.RenderPageToBitmap(page);
            Convert.ToPdf(doc, inputPath);
            ocr.StartOCR(image);
            """;
        var sut = new FoxitPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Existing-PDF editing, forms, annotations, security/signing, rendering, viewer, OCR/conversion, or redaction APIs require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Severity == MigrationDiagnosticSeverity.Warning
            && diagnostic.Id == "CANMIGFOXIT021");
    }
}
