using Canvas.Migration.Abstractions;
using Canvas.Migration.AsposePdf;

namespace Canvas.Migration.AsposePdf.Tests;

public sealed class AsposePdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertBasicDocumentTextAndSave()
    {
        var source = """
            using Aspose.Pdf;
            using Aspose.Pdf.Text;

            var document = new Document();
            var page = document.Pages.Add();
            var text = new TextFragment("Hello");
            page.Paragraphs.Add(text);
            document.Save(path);
            """;
        var expected = """
            using Canvas.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Hello", 40, 40, 12);
            document.Save(path);
            """;
        var sut = new AsposePdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE007");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE008");
    }

    [Fact]
    public void Migrate_ShouldConvertInlineTextFragmentParagraph()
    {
        var source = """
            using Aspose.Pdf;
            using Aspose.Pdf.Text;

            var document = new Document();
            var page = document.Pages.Add();
            page.Paragraphs.Add(new TextFragment("Inline"));
            document.Save(stream);
            """;
        var expected = """
            using Canvas.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Inline", 40, 40, 12);
            document.Save(stream);
            """;
        var sut = new AsposePdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE003");
    }

    [Fact]
    public void Migrate_ShouldConvertPositionedTextFragment()
    {
        var source = """
            using Aspose.Pdf;
            using Aspose.Pdf.Text;

            var document = new Document();
            var page = document.Pages.Add();
            var text = new TextFragment("Positioned");
            text.Position = new Position(72, 700);
            page.Paragraphs.Add(text);
            document.Save(path);
            """;
        var expected = """
            using Canvas.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawText("Positioned", 72, 700, 12);
            document.Save(path);
            """;
        var sut = new AsposePdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE010");
    }

    [Fact]
    public void Migrate_ShouldConvertTextBuilderAppendText()
    {
        var source = """
            using Aspose.Pdf;
            using Aspose.Pdf.Text;

            var document = new Document();
            var page = document.Pages.Add();
            var builder = new TextBuilder(page);
            builder.AppendText(new TextFragment("Builder"));
            document.Save(path);
            """;
        var expected = """
            using Canvas.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Builder", 40, 40, 12);
            document.Save(path);
            """;
        var sut = new AsposePdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE004");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE009");
    }

    [Fact]
    public void Migrate_ShouldWarnAndRemoveSimpleTextStateAssignment()
    {
        var source = """
            using Aspose.Pdf;
            using Aspose.Pdf.Text;

            var document = new Document();
            var page = document.Pages.Add();
            var text = new TextFragment("Styled");
            text.TextState.FontSize = 18;
            page.Paragraphs.Add(text);
            document.Save(path);
            """;
        var expected = """
            using Canvas.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Styled", 40, 40, 12);
            document.Save(path);
            """;
        var sut = new AsposePdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGASPOSE011"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldKeepTextFragmentWhenUsageIsUnsupported()
    {
        var source = """
            using Aspose.Pdf;
            using Aspose.Pdf.Text;

            var document = new Document();
            var page = document.Pages.Add();
            var text = new TextFragment("Unsupported");
            text.Hyperlink = new WebHyperlink("https://example.test");
            page.Paragraphs.Add(text);
            document.Save(path);
            """;
        var sut = new AsposePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var text = new TextFragment(\"Unsupported\");", result.MigratedCode);
        Assert.Contains("text.Hyperlink = new WebHyperlink(\"https://example.test\");", result.MigratedCode);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGASPOSE008");
    }

    [Fact]
    public void Migrate_ShouldWarnForTablesAndSecurityApis()
    {
        var source = """
            using Aspose.Pdf;
            using Aspose.Pdf.Facades;

            var table = new Table();
            var security = new PdfFileSecurity();
            """;
        var sut = new AsposePdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("new Table()", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGASPOSE020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGASPOSE021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
