using PXA.Migration.Abstractions;

namespace PXA.Migration.iText7.Tests;

public sealed class IText7MigrationTests
{
    [Fact]
    public void Migrate_HelloWorldDocument_UsesPxaMigrationResult()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Layout;
            using iText.Layout.Element;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            document.Add(new Paragraph("Hello"));
            """;
        var sut = new IText7Migration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using Canvas.Pdf;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT007");
    }

    [Fact]
    public void Migrate_MapsCanvasWarningsToPxaDiagnostics()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Layout;
            using iText.Layout.Element;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            document.Add(new Table(3));
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGITEXT005" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
