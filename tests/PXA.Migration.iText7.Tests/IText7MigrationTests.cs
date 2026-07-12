using PXA.Migration.Abstractions;
using PXA.Migration.iText7;

namespace PXA.Migration.iText7.Tests;

public sealed class IText7MigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertHelloWorldDocument()
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
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Hello", 40, 40, 12);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT004");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT007");
    }

    [Fact]
    public void Migrate_ShouldWarnForTableUsage()
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

        Assert.Contains("document.Add(new Table(3));", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGITEXT005"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForSecurityAndForms()
    {
        var source = """
            using iText.Forms;
            using iText.Signatures;

            var signer = new PdfSigner(reader, outputStream, stampingProperties);
            var form = PdfAcroForm.GetAcroForm(pdf, true);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Contains("new PdfSigner(reader, outputStream, stampingProperties)", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGITEXT006"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldConvertDocumentPageSize()
    {
        var source = """
            using iText.Kernel.Geom;
            using iText.Kernel.Pdf;
            using iText.Layout;
            using iText.Layout.Element;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4.Rotate());
            document.Add(new Paragraph("Landscape"));
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage(PdfPagePreset.A4, true);
            page.DrawTextFromTop("Landscape", 40, 40, 12);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT008");
    }

    [Fact]
    public void Migrate_ShouldConvertRealisticInvoiceFixtureEndToEnd()
    {
        var source = """
            using iText.Kernel.Geom;
            using iText.Kernel.Pdf;
            using iText.Layout;
            using iText.Layout.Element;
            using iText.Layout.Properties;

            using var writer = new PdfWriter(outputStream);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.LETTER);
            document.Add(new Paragraph("Invoice"));
            document.ShowTextAligned(new Paragraph("INV-1001"), 430, 720, TextAlignment.LEFT);
            document.Add(new Paragraph("Customer"));
            document.Add(new Table(3));
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage(PdfPagePreset.Letter, false);
            page.DrawTextFromTop("Invoice", 40, 40, 12);
            page.DrawText("INV-1001", 430, 720, 12);
            page.DrawTextFromTop("Customer", 40, 40, 12);
            document.Add(new Table(3));
            document.Save(outputStream);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT008");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT009");
    }

    [Fact]
    public void Migrate_ShouldConvertCombinedV1Fixture()
    {
        var source = """
            using iText.Kernel.Geom;
            using iText.Kernel.Pdf;
            using iText.Kernel.Pdf.Canvas;
            using iText.Layout;
            using iText.Layout.Element;
            using iText.Layout.Properties;

            using var writer = new PdfWriter(outputStream);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4.Rotate());
            document.Add(new Paragraph("Invoice"));
            document.ShowTextAligned(new Paragraph("INV-1001"), 430, 520, TextAlignment.LEFT);
            var canvas = new PdfCanvas(pdf.GetFirstPage());
            canvas.MoveTo(40, 500).LineTo(760, 500).Stroke();
            canvas.Rectangle(40, 380, 220, 80).Fill();
            canvas.BeginText();
            canvas.MoveText(72, 420);
            canvas.ShowText("Total");
            canvas.EndText();
            document.Add(new Table(3));
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage(PdfPagePreset.A4, true);
            page.DrawTextFromTop("Invoice", 40, 40, 12);
            page.DrawText("INV-1001", 430, 520, 12);
            page.DrawLine(40, 500, 760, 500, 1);
            page.DrawRectangle(40, 380, 220, 80, 1, true);
            page.DrawText("Total", 72, 420, 12);
            document.Add(new Table(3));
            document.Save(outputStream);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT008");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT009");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT011");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT012");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT013");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT015");
    }

    [Fact]
    public void Migrate_ShouldConvertLeftShowTextAligned()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Layout;
            using iText.Layout.Element;
            using iText.Layout.Properties;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            document.ShowTextAligned(new Paragraph("Positioned"), 72, 700, TextAlignment.LEFT);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawText("Positioned", 72, 700, 12);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT009");
    }

    [Fact]
    public void Migrate_ShouldWarnForCenteredShowTextAligned()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Layout;
            using iText.Layout.Element;
            using iText.Layout.Properties;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            document.ShowTextAligned(new Paragraph("Centered"), 300, 700, TextAlignment.CENTER);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Contains("document.ShowTextAligned(new Paragraph(\"Centered\"), 300, 700, TextAlignment.CENTER);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGITEXT010"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldConvertPdfCanvasLine()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Kernel.Pdf.Canvas;
            using iText.Layout;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            var canvas = new PdfCanvas(pdf.GetFirstPage());
            canvas.MoveTo(40, 700).LineTo(555, 700).Stroke();
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawLine(40, 700, 555, 700, 1);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT011");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT013");
    }

    [Fact]
    public void Migrate_ShouldConvertPdfCanvasRectangles()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Kernel.Pdf.Canvas;
            using iText.Layout;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            var canvas = new PdfCanvas(pdf.GetFirstPage());
            canvas.Rectangle(40, 620, 200, 80).Stroke();
            canvas.Rectangle(40, 500, 200, 80).Fill();
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawRectangle(40, 620, 200, 80, 1, false);
            page.DrawRectangle(40, 500, 200, 80, 1, true);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT012");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT013");
    }

    [Fact]
    public void Migrate_ShouldKeepPdfCanvasVariableWhenAnyUsageIsUnsupported()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Kernel.Pdf.Canvas;
            using iText.Layout;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            var canvas = new PdfCanvas(pdf.GetFirstPage());
            canvas.MoveTo(40, 700).LineTo(555, 700).Stroke();
            canvas.CurveTo(1, 2, 3, 4, 5, 6).Stroke();
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Contains("var canvas = new PdfCanvas(pdf.GetFirstPage());", result.MigratedCode);
        Assert.Contains("canvas.CurveTo(1, 2, 3, 4, 5, 6).Stroke();", result.MigratedCode);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT013");
    }

    [Fact]
    public void Migrate_ShouldConvertPdfCanvasTextChain()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Kernel.Pdf.Canvas;
            using iText.Layout;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            var canvas = new PdfCanvas(pdf.GetFirstPage());
            canvas.BeginText().MoveText(72, 700).ShowText("PXA text").EndText();
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawText("PXA text", 72, 700, 12);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT014");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT013");
    }

    [Fact]
    public void Migrate_ShouldRemovePdfCanvasVariableWhenTextAndShapeUsagesAreSupported()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Kernel.Pdf.Canvas;
            using iText.Layout;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            var canvas = new PdfCanvas(pdf.GetFirstPage());
            canvas.MoveTo(40, 700).LineTo(555, 700).Stroke();
            canvas.BeginText().MoveText(72, 650).ShowText("Mixed").EndText();
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawLine(40, 700, 555, 700, 1);
            page.DrawText("Mixed", 72, 650, 12);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT011");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT014");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT013");
    }

    [Fact]
    public void Migrate_ShouldConvertSeparatedPdfCanvasTextStateStatements()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Kernel.Pdf.Canvas;
            using iText.Layout;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            var canvas = new PdfCanvas(pdf.GetFirstPage());
            canvas.BeginText();
            canvas.MoveText(72, 700);
            canvas.ShowText("Separated");
            canvas.EndText();
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawText("Separated", 72, 700, 12);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT015");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT013");
    }

    [Fact]
    public void Migrate_ShouldKeepSeparatedPdfCanvasTextStateWhenSequenceIsIncomplete()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Kernel.Pdf.Canvas;
            using iText.Layout;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            var canvas = new PdfCanvas(pdf.GetFirstPage());
            canvas.BeginText();
            canvas.ShowText("Missing position");
            canvas.EndText();
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Contains("var canvas = new PdfCanvas(pdf.GetFirstPage());", result.MigratedCode);
        Assert.Contains("canvas.ShowText(\"Missing position\");", result.MigratedCode);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT015");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGITEXT013");
    }

    [Fact]
    public void Migrate_ShouldRemoveDocumentClose()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Layout;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            document.Add(new Paragraph("Hello"));
            document.Close();
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Hello", 40, 40, 12);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT016");
    }

    [Fact]
    public void Migrate_ShouldRemoveDocumentSetMargins()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Layout;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            document.SetMargins(72, 72, 72, 72);
            document.Add(new Paragraph("Hello"));
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Hello", 40, 40, 12);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT017");
    }

    [Fact]
    public void Migrate_ShouldPreserveFontSizeFromParagraphSetFontSize()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Layout;
            using iText.Layout.Element;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            document.Add(new Paragraph("Invoice").SetFontSize(18));
            document.Add(new Paragraph("Details").SetFontSize(10));
            document.Add(new Paragraph("Footer"));
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Invoice", 40, 40, 18);
            page.DrawTextFromTop("Details", 40, 40, 10);
            page.DrawTextFromTop("Footer", 40, 40, 12);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT018");
    }

    [Fact]
    public void Migrate_ShouldIgnoreOtherParagraphStylingAndExtractText()
    {
        var source = """
            using iText.Kernel.Pdf;
            using iText.Layout;
            using iText.Layout.Element;

            using var writer = new PdfWriter(path);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);
            document.Add(new Paragraph("Bold heading").SetFontSize(16).SetBold());
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Bold heading", 40, 40, 16);
            document.Save(path);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT018");
    }

    [Fact]
    public void Migrate_ShouldConvertFullInvoiceWithAllNewFeatures()
    {
        var source = """
            using iText.Kernel.Geom;
            using iText.Kernel.Pdf;
            using iText.Kernel.Pdf.Canvas;
            using iText.Layout;
            using iText.Layout.Element;
            using iText.Layout.Properties;

            using var writer = new PdfWriter(outputPath);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf, PageSize.A4);
            document.SetMargins(72, 72, 72, 72);
            document.Add(new Paragraph("Invoice #2024").SetFontSize(18));
            document.Add(new Paragraph("Thank you for your order."));
            document.ShowTextAligned(new Paragraph("Total: $150.00"), 400, 100, TextAlignment.LEFT);
            var canvas = new PdfCanvas(pdf.GetFirstPage());
            canvas.MoveTo(72, 700).LineTo(524, 700).Stroke();
            canvas.BeginText();
            canvas.MoveText(80, 620);
            canvas.ShowText("Item Details");
            canvas.EndText();
            document.Close();
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage(PdfPagePreset.A4, false);
            page.DrawTextFromTop("Invoice #2024", 40, 40, 18);
            page.DrawTextFromTop("Thank you for your order.", 40, 40, 12);
            page.DrawText("Total: $150.00", 400, 100, 12);
            page.DrawLine(72, 700, 524, 700, 1);
            page.DrawText("Item Details", 80, 620, 12);
            document.Save(outputPath);
            """;
        var sut = new IText7Migration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT008");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT016");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT017");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGITEXT018");
    }
}
