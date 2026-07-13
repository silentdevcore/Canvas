using PXA.Migration.Abstractions;

namespace PXA.Migration.Pdf.Code.Syncfusion.Tests;

public sealed class SyncfusionPdfMigrationPxaFacadeTests
{
    [Fact]
    public void Migrate_HelloWorldDocument_UsesPxaMigrationResult()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            using var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawString("Hello", new PdfStandardFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 40, 40);
            document.Save(path);
            """;
        var sut = new SyncfusionPdfMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12, PdfFontFamily.Helvetica);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGSYNC001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGSYNC003");
    }

    [Fact]
    public void Migrate_MapsPxaWarningsToPxaDiagnostics()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Grid;

            var document = new PdfDocument();
            var grid = new PdfGrid();
            grid.Draw(document.Pages.Add(), PointF.Empty);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGSYNC005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
