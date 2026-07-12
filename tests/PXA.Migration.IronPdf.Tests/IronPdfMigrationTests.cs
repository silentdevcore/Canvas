using PXA.Migration.Abstractions;
using PXA.Migration.IronPdf;

namespace PXA.Migration.IronPdf.Tests;

public sealed class IronPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertBasicHtmlRenderWorkflowToCanvasScaffold()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlAsPdf("<h1>Hello</h1>");
            pdf.SaveAs(outputPath);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using IronPdf", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.DoesNotContain("ChromePdfRenderer", result.MigratedCode);
        Assert.DoesNotContain("RenderHtmlAsPdf", result.MigratedCode);
        Assert.DoesNotContain("SaveAs", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF001" && d.Severity == MigrationDiagnosticSeverity.Info);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF002" && d.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF006" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Migrate_ShouldIncludeHtmlContentInDiagnosticForLiteralHtml()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlAsPdf("<h1>Invoice</h1><p>Total: $150</p>");
            pdf.SaveAs(path);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        var renderDiagnostic = result.Diagnostics.First(d => d.Id == "CANMIGIRONPDF002");
        Assert.Contains("<h1>Invoice</h1><p>Total: $150</p>", renderDiagnostic.Message);
    }

    [Fact]
    public void Migrate_ShouldWarnForDynamicHtmlRendering()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var html = template.Render(model);
            var pdf = renderer.RenderHtmlAsPdf(html);
            pdf.SaveAs(path);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF002" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldHandleChainedRendererCreationAndRenderCall()
    {
        var source = """
            using IronPdf;

            var pdf = new ChromePdfRenderer().RenderHtmlAsPdf("<h1>Chained</h1>");
            pdf.SaveAs(outputPath);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.DoesNotContain("ChromePdfRenderer", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF002");
    }

    [Fact]
    public void Migrate_ShouldWarnForHtmlFileRendering()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlFileAsPdf("template.html");
            pdf.SaveAs("output.pdf");
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.DoesNotContain("RenderHtmlFileAsPdf", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF003" && d.Severity == MigrationDiagnosticSeverity.Warning);
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

        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.DoesNotContain("RenderUrlAsPdf", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF004" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForRazorRendering()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderRazorToPdf(view, model);
            pdf.SaveAs(path);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.DoesNotContain("RenderRazorToPdf", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldConvertAsyncSaveToSyncSave()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlAsPdf("<p>Hello</p>");
            await pdf.SaveAsAsync(outputPath);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.DoesNotContain("SaveAsAsync", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF007" && d.Severity == MigrationDiagnosticSeverity.Info);
    }

    [Fact]
    public void Migrate_ShouldRemoveRendererOptionAssignments()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            renderer.RenderingOptions.MarginTop = 20;
            renderer.RenderingOptions.MarginBottom = 20;
            var pdf = renderer.RenderHtmlAsPdf("<p>Hello</p>");
            pdf.SaveAs(path);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.DoesNotContain("RenderingOptions", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldWarnForEditingAndSecurityApis()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlAsPdf("<p>Signed</p>");
            pdf.SignPdfWithDigitalSignature(signature);
            pdf.SaveAs(path);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGIRONPDF020" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldConvertLegacyHtmlToPdfRenderer()
    {
        var source = """
            using IronPdf;

            var renderer = new HtmlToPdf();
            var pdf = renderer.RenderHtmlAsPdf("<p>Legacy</p>");
            pdf.SaveAs(outputPath);
            """;
        var sut = new IronPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.DoesNotContain("HtmlToPdf", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF001");
    }
}
