using Canvas.Migration.Abstractions;
using Canvas.Migration.IronPdf;

namespace Canvas.Migration.IronPdf.Tests;

public sealed class IronPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldReportHtmlRenderingAndPreserveSource()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlAsPdf("<h1>Hello</h1>");
            pdf.SaveAs(path);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("// Canvas.Pdf migration report: IronPDF", result.MigratedCode);
        Assert.Contains("Literal HTML detected for manual extraction: <h1>Hello</h1>", result.MigratedCode);
        Assert.Contains("var pdf = renderer.RenderHtmlAsPdf(\"<h1>Hello</h1>\");", result.MigratedCode);
        Assert.Contains("pdf.SaveAs(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGIRONPDF001");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGIRONPDF002"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGIRONPDF006");
    }

    [Fact]
    public void Migrate_ShouldWarnForDynamicHtmlRendering()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var html = template.Render(model);
            var pdf = renderer.RenderHtmlAsPdf(html);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("HTML source is dynamic; inspect template/data flow before rewriting.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGIRONPDF002"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForUrlRendering()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderUrlAsPdf("https://example.test/report");
            pdf.SaveAs("report.pdf");
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("URL-to-PDF rendering is outside direct Canvas.Pdf source migration.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGIRONPDF004"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForHtmlFileRendering()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlFileAsPdf("template.html");
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("HTML file rendering requires manual template review", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGIRONPDF003"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForRazorRendering()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderRazorToPdf(view, model);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Razor-to-PDF rendering requires manual view/template migration.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGIRONPDF005"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldDetectAsyncSave()
    {
        var source = """
            using IronPdf;

            await pdf.SaveAsAsync(path);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("SaveAsAsync(...)", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGIRONPDF007");
    }

    [Fact]
    public void Migrate_ShouldWarnForEditingAndSecurityApis()
    {
        var source = """
            using IronPdf;

            var pdf = PdfDocument.FromFile(path);
            pdf.SecuritySettings.AllowUserAnnotations = false;
            pdf.SignPdfWithDigitalSignature(signature);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("PDF editing/merge/security/signing APIs require manual migration outside v1.", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGIRONPDF020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
