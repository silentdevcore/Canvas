using Canvas.Migration.Abstractions;
using Canvas.Migration.DsPdf;

namespace Canvas.Migration.DsPdf.Tests;

public sealed class DsPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldReportBasicDocumentPageTextAndSaveWorkflow()
    {
        var source = """
            using GrapeCity.Documents.Pdf;
            using GrapeCity.Documents.Drawing;

            var document = new GcPdfDocument();
            var page = document.NewPage();
            page.Graphics.DrawString("Hello", new TextFormat(), new PointF(40, 40));
            document.Save(path);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("// Canvas.Pdf migration report: DsPdf / Document Solutions", result.MigratedCode);
        Assert.Contains("new GcPdfDocument(...) detected", result.MigratedCode);
        Assert.Contains("NewPage(...) detected", result.MigratedCode);
        Assert.Contains("TextFormat detected", result.MigratedCode);
        Assert.Contains("DrawString(...) detected", result.MigratedCode);
        Assert.Contains("Save(...) detected", result.MigratedCode);
        Assert.Contains("var document = new GcPdfDocument();", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDSPDF001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDSPDF002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDSPDF003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDSPDF004");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDSPDF007");
    }

    [Fact]
    public void Migrate_ShouldReportImageAndShapeDrawingCandidates()
    {
        var source = """
            using DS.Documents.Pdf;

            page.Graphics.DrawImage(image, new RectangleF(40, 120, 200, 80));
            page.Graphics.DrawLine(pen, 40, 700, 555, 700);
            page.Graphics.DrawRectangle(pen, new RectangleF(40, 620, 200, 80));
            page.Graphics.FillRectangle(brush, new RectangleF(40, 500, 200, 40));
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("DrawImage(...) detected", result.MigratedCode);
        Assert.Contains("DrawLine(...) detected", result.MigratedCode);
        Assert.Contains("DrawRectangle(...) detected", result.MigratedCode);
        Assert.Contains("FillRectangle(...) detected", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDSPDF005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDSPDF006");
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

        Assert.Contains("Advanced layout, AcroForms, annotations, PDF/A/compliance, redaction, signatures, security, or existing-PDF editing require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGDSPDF020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGDSPDF023"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForExistingPdfEditingAndMerge()
    {
        var source = """
            using GrapeCity.Documents.Pdf;

            var document = new GcPdfDocument();
            document.Load(inputPath);
            document.DeletePage(1);
            document.MergeWithDocument(otherDocument);
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Advanced layout, AcroForms, annotations, PDF/A/compliance, redaction, signatures, security, or existing-PDF editing require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDSPDF001");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGDSPDF021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForFormsComplianceSecurityAndRedaction()
    {
        var source = """
            using GrapeCity.Documents.Pdf;

            document.AcroForm.Fields.Add(field);
            document.SaveAsPdfA(path);
            document.Sign(signatureProperties);
            document.SetSecurity(security);
            page.Annotations.Add(annotation);
            document.ApplyRedactions();
            """;
        var sut = new DsPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Advanced layout, AcroForms, annotations, PDF/A/compliance, redaction, signatures, security, or existing-PDF editing require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGDSPDF022"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGDSPDF023"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
