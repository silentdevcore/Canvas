using PXA.Migration.Abstractions;

namespace PXA.Migration.IronPdf.Tests;

public sealed class IronPdfMigrationTests
{
    [Fact]
    public void Migrate_BasicHtmlRenderWorkflow_UsesPxaMigrationResult()
    {
        var source = """
            using IronPdf;

            var renderer = new ChromePdfRenderer();
            var pdf = renderer.RenderHtmlAsPdf("<h1>Hello</h1>");
            pdf.SaveAs(outputPath);
            """;
        var sut = new IronPdfMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("using IronPdf", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGIRONPDF006");
    }

    [Fact]
    public void Migrate_MapsCanvasWarningsToPxaDiagnostics()
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
}
