using PXA.Migration.Abstractions;

namespace PXA.Migration.Pdf.Code.PdfToolsToolbox.Tests;

public sealed class PdfToolsToolboxMigrationPxaFacadeTests
{
    [Fact]
    public void Migrate_SimpleFromScratchTextLayout_UsesPxaMigrationResult()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;
            using PdfTools.Toolbox.Pdf.Content;
            using PdfTools.Toolbox.Pdf.Content.Text;

            using var outStream = new FileStream(outPath, FileMode.CreateNew, FileAccess.ReadWrite);
            using var outDoc = Document.Create(outStream, null, null);
            var font = Font.CreateFromSystem(outDoc, "Arial", "Italic", true);
            var outPage = Page.Create(outDoc, PageSize);
            using var gen = new ContentGenerator(outPage.Content, false);
            var text = Text.Create(outDoc);
            using var textGenerator = new TextGenerator(text, font, 20, null);
            textGenerator.MoveTo(new Point { X = 72, Y = outPage.Size.Height - 72 });
            textGenerator.ShowLine("Hello from Toolbox");
            gen.PaintText(text);
            outDoc.Pages.Add(outPage);
            """;
        var sut = new PdfToolsToolboxMigration();

        MigrationResult result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("PdfTools.Toolbox", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var outPage = document.AddPage();", result.MigratedCode);
        Assert.Contains("outPage.DrawTextFromTop(\"Hello from Toolbox\", 72, 72, 20);", result.MigratedCode);
        Assert.Contains("document.Save(outPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGPDFTOOLBOX000");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGPDFTOOLBOX007");
    }

    [Fact]
    public void Migrate_MapsPxaWarningsToPxaDiagnostics()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;
            using PdfTools.Toolbox.Pdf.Content;

            var colorSpace = ColorSpace.CreateProcessColorSpace(outDoc, colorType);
            var paint = Paint.Create(outDoc, colorSpace, color, transparency);
            var fill = new Fill(paint);
            var image = Image.Create(outDoc, imageStream);
            """;
        var sut = new PdfToolsToolboxMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGPDFTOOLBOX004" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
