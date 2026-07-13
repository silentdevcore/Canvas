using PXA.Migration.Abstractions;
using PXA.Migration.Pdf.Code.PdfToolsToolbox;

namespace PXA.Migration.Pdf.Code.PdfToolsToolbox.Tests;

public sealed class PdfToolsToolboxMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertSimpleFromScratchTextLayout()
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

        var result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("PdfTools.Toolbox", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var outPage = document.AddPage();", result.MigratedCode);
        Assert.Contains("outPage.DrawTextFromTop(\"Hello from Toolbox\", 72, 72, 20);", result.MigratedCode);
        Assert.Contains("document.Save(outPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLBOX000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLBOX001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLBOX002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLBOX003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLBOX007");
    }

    [Fact]
    public void Migrate_ShouldConvertPointVariablePosition()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;
            using PdfTools.Toolbox.Pdf.Content;
            using PdfTools.Toolbox.Pdf.Content.Text;

            using var outStream = File.Create(path);
            using var outDoc = Document.Create(outStream, null, null);
            var outPage = Page.Create(outDoc, PageSize);
            var text = Text.Create(outDoc);
            using var textGenerator = new TextGenerator(text, font, fontSize, null);
            var position = new Point { X = left, Y = outPage.Size.Height - top };
            textGenerator.MoveTo(position);
            textGenerator.ShowLine(title);
            outDoc.Pages.Add(outPage);
            """;
        var sut = new PdfToolsToolboxMigration();

        var result = sut.Migrate(source);

        Assert.Contains("outPage.DrawTextFromTop(title, left, top, fontSize);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldMapKnownPageSizes()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;

            using var outStream = File.Create(path);
            using var outDoc = Document.Create(outStream, null, null);
            var outPage = Page.Create(outDoc, PageSize.A4.Rotate());
            outDoc.Pages.Add(outPage);
            """;
        var sut = new PdfToolsToolboxMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var outPage = document.AddPage(PdfPagePreset.A4, true);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldWarnForUnknownPageSizes()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;

            using var outStream = File.Create(path);
            using var outDoc = Document.Create(outStream, null, null);
            var outPage = Page.Create(outDoc, customSize);
            outDoc.Pages.Add(outPage);
            """;
        var sut = new PdfToolsToolboxMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var outPage = document.AddPage();", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLBOX009"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnWhenOutputPathIsNotSafeToInfer()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;
            using PdfTools.Toolbox.Pdf.Content.Text;

            using var outDoc = Document.Create(outputStream, null, null);
            var outPage = Page.Create(outDoc, PageSize);
            var text = Text.Create(outDoc);
            using var textGenerator = new TextGenerator(text, font, 20, null);
            textGenerator.ShowLine("Needs save");
            outDoc.Pages.Add(outPage);
            """;
        var sut = new PdfToolsToolboxMigration();

        var result = sut.Migrate(source);

        Assert.DoesNotContain("document.Save(", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLBOX008"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForFontColorPaintAndImages()
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

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLBOX004"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.DoesNotContain("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains("using PdfTools.Toolbox.Pdf;", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldWarnForExistingPdfCopyWorkflows()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;

            using var inDoc = Document.Open(inStream, null);
            var options = new PageCopyOptions();
            var outPage = Page.Copy(outDoc, inDoc.Pages[0], options);
            outDoc.Pages.Add(outPage);
            """;
        var sut = new PdfToolsToolboxMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Document.Open", result.MigratedCode);
        Assert.DoesNotContain("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains("using PdfTools.Toolbox.Pdf;", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLBOX020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldPreserveToolboxUsingsWhenPartiallyMigratedCodeRemains()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;
            using PdfTools.Toolbox.Pdf.Annotations;

            using var outStream = File.Create(path);
            using var outDoc = Document.Create(outStream, null, null);
            var outPage = Page.Create(outDoc, PageSize.A4);
            var annotation = new Annotation();
            outDoc.Pages.Add(outPage);
            """;
        var sut = new PdfToolsToolboxMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains("using PdfTools.Toolbox.Pdf;", result.MigratedCode);
        Assert.Contains("using PdfTools.Toolbox.Pdf.Annotations;", result.MigratedCode);
        Assert.Contains("var outPage = document.AddPage(PdfPagePreset.A4, false);", result.MigratedCode);
        Assert.Contains("var annotation = new Annotation();", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLBOX010"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForFormsAnnotationsMetadataAndTagging()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;
            using PdfTools.Toolbox.Pdf.Annotations;
            using PdfTools.Toolbox.Pdf.Forms;

            var annotation = new Annotation();
            var form = new Form();
            outDoc.Metadata.Title = "TaggedPDF";
            outDoc.ViewerSettings.DisplayDocumentTitle = true;
            """;
        var sut = new PdfToolsToolboxMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLBOX006"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
