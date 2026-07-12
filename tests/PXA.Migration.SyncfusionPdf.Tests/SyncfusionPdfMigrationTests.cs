using PXA.Migration.Abstractions;
using PXA.Migration.SyncfusionPdf;

namespace PXA.Migration.SyncfusionPdf.Tests;

public sealed class SyncfusionPdfMigrationTests
{
    [Fact]
    public void Migrate_ShouldConvertHelloWorldDocument()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            using var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawString("Hello", new PdfStandardFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 40, 40);
            document.Save(path);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Hello", 40, 40, 12, PdfFontFamily.Helvetica);
            document.Save(path);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC003");
    }

    [Fact]
    public void Migrate_ShouldKeepUnsupportedDrawStringUnchanged()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawString("Hello", font, PdfBrushes.Black, 40, 40);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.Graphics.DrawString(\"Hello\", font, PdfBrushes.Black, 40, 40);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldConvertSupportedPdfGraphicsVariable()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            PdfGraphics graphics = page.Graphics;
            graphics.DrawString("Invoice", new PdfStandardFont(PdfFontFamily.Courier, 10), PdfBrushes.Blue, 24, 32);
            document.Save(stream);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Invoice", 24, 32, 10, PdfFontFamily.Courier);
            document.Save(stream);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC009");
    }

    [Fact]
    public void Migrate_ShouldKeepPdfGraphicsVariable_WhenAnyUsageIsUnsupported()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            PdfGraphics graphics = page.Graphics;
            graphics.DrawString("Invoice", font, PdfBrushes.Blue, 24, 32);
            document.Save(stream);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("PdfGraphics graphics = page.Graphics;", result.MigratedCode);
        Assert.Contains("graphics.DrawString(\"Invoice\", font, PdfBrushes.Blue, 24, 32);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldConvertSimpleLineAndRectangleCalls()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawLine(PdfPens.Black, 40, 40, 160, 40);
            page.Graphics.DrawRectangle(PdfPens.Blue, 40, 60, 120, 50);
            page.Graphics.DrawRectangle(PdfBrushes.Green, 40, 130, 120, 50);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawLineFromTop(40, 40, 160, 40, 1, PdfColor.Black);
            page.DrawRectangleFromTop(40, 60, 120, 50, 1, false, PdfColor.BlueColor);
            page.DrawRectangleFromTop(40, 130, 120, 50, 1, true, null, PdfColor.GreenColor);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC010");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC011");
    }

    [Fact]
    public void Migrate_ShouldRemoveGraphicsVariable_WhenAllShapeCallsAreSupported()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            PdfGraphics graphics = page.Graphics;
            graphics.DrawLine(PdfPens.Red, 10, 20, 30, 40);
            graphics.DrawRectangle(PdfBrushes.Blue, 50, 60, 70, 80);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawLineFromTop(10, 20, 30, 40, 1, PdfColor.RedColor);
            page.DrawRectangleFromTop(50, 60, 70, 80, 1, true, null, PdfColor.BlueColor);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC009");
    }

    [Fact]
    public void Migrate_ShouldConvertRectangleDrawStringWithoutFormat()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawString("Wrapped", new PdfStandardFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, new RectangleF(40, 40, 200, 80));
            """;
        var expected = """
            using PXA.Pdf;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextBoxFromTop("Wrapped", 40, 40, 200, 80, new PdfTextBoxOptions { FontFamily = PdfFontFamily.Helvetica, FontSize = 12, FillColor = PdfColor.Black });
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC012");
    }

    [Fact]
    public void Migrate_ShouldConvertRectangleDrawStringWithInlineFormat()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawString("Wrapped", new PdfStandardFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, new RectangleF(40, 40, 200, 80), new PdfStringFormat { Alignment = PdfTextAlignment.Center });
            """;
        var expected = """
            using PXA.Pdf;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextBoxFromTop("Wrapped", 40, 40, 200, 80, new PdfTextBoxOptions { FontFamily = PdfFontFamily.Helvetica, FontSize = 12, FillColor = PdfColor.Black, Alignment = PdfTextAlignment.Center });
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldConvertRectangleDrawStringWithFormatVariable()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            var format = new PdfStringFormat { Alignment = PdfTextAlignment.Right, LineAlignment = PdfVerticalAlignment.Bottom };
            page.Graphics.DrawString("Wrapped", new PdfStandardFont(PdfFontFamily.Courier, 10), PdfBrushes.Blue, new RectangleF(40, 40, 200, 80), format);
            """;
        var expected = """
            using PXA.Pdf;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextBoxFromTop("Wrapped", 40, 40, 200, 80, new PdfTextBoxOptions { FontFamily = PdfFontFamily.Courier, FontSize = 10, FillColor = PdfColor.BlueColor, Alignment = PdfTextAlignment.Right, VerticalAlignment = PdfVerticalAlignment.Bottom });
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC013");
    }

    [Fact]
    public void Migrate_ShouldConvertImageFromFileDrawImage()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawImage(PdfImage.FromFile(imagePath), 40, 200, 80, 40);
            page.Graphics.DrawImage(PdfImage.FromFile(imagePath), 40, 260);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawImageFromTop(imagePath, 40, 200, 80, 40);
            page.DrawImageFromTop(imagePath, 40, 260);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC014");
    }

    [Fact]
    public void Migrate_ShouldConvertPdfBitmapStreamDrawImage()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawImage(new PdfBitmap(imageStream), 140, 200, 80, 40);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawImageFromTop(imageStream, 140, 200, 80, 40);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldConvertPdfBitmapByteArrayDrawImage()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawImage(new PdfBitmap(imageBytes), 140, 200, 80, 40);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawImageFromTop(imageBytes, 140, 200, 80, 40);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldRemoveGraphicsVariable_WhenAllImageCallsAreSupported()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            PdfGraphics graphics = page.Graphics;
            graphics.DrawImage(PdfImage.FromFile(imagePath), 40, 200, 80, 40);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawImageFromTop(imagePath, 40, 200, 80, 40);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC009");
    }

    [Fact]
    public void Migrate_ShouldConvertSolidBrushAndPenFromArgb()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawRectangle(new PdfSolidBrush(Color.FromArgb(230, 240, 255)), 40, 190, 120, 40);
            page.Graphics.DrawLine(new PdfPen(Color.FromArgb(10, 20, 30), 2), 40, 40, 160, 40);
            """;
        var expected = """
            using PXA.Pdf;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawRectangleFromTop(40, 190, 120, 40, 1, true, null, PdfColor.FromRgb(230, 240, 255));
            page.DrawLineFromTop(40, 40, 160, 40, 2, PdfColor.FromRgb(10, 20, 30));
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
    }

    [Fact]
    public void Migrate_ShouldRemoveDocumentCloseTrueAfterSave()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawString("Hello", new PdfStandardFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 40, 40);
            document.Save(stream);
            document.Close(true);
            """;
        var expected = """
            using PXA.Pdf;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextFromTop("Hello", 40, 40, 12, PdfFontFamily.Helvetica);
            document.Save(stream);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC015");
    }

    [Fact]
    public void Migrate_ShouldKeepDocumentCloseTrueWhenDocumentWasNotSaved()
    {
        var source = """
            using Syncfusion.Pdf;

            var document = new PdfDocument();
            document.Close(true);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("document.Close(true);", result.MigratedCode);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC015");
    }

    [Fact]
    public void Migrate_ShouldWarnForComplexRectangleDrawStringFormat()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.Pages.Add();
            page.Graphics.DrawString("Wrapped", new PdfStandardFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, new RectangleF(40, 40, 200, 80), new PdfStringFormat { Alignment = PdfTextAlignment.Center, WordWrap = PdfWordWrapType.Word });
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("WordWrap = PdfWordWrapType.Word", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGSYNC004"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForPdfGridUsage()
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

        Assert.Contains("new PdfGrid()", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGSYNC005"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForOutOfScopePdfProcessingFeatures()
    {
        var source = """
            using Syncfusion.Pdf.Parsing;
            using Syncfusion.Pdf.Security;

            var loaded = new PdfLoadedDocument(inputStream);
            var security = new PdfDocumentSecurity();
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("new PdfLoadedDocument(inputStream)", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGSYNC006"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldConvertRealisticInvoiceFixtureEndToEnd()
    {
        var source = """
            using Syncfusion.Pdf;
            using Syncfusion.Pdf.Graphics;
            using Syncfusion.Drawing;

            using var document = new PdfDocument();
            var page = document.Pages.Add();
            PdfGraphics graphics = page.Graphics;
            var titleFormat = new PdfStringFormat { Alignment = PdfTextAlignment.Center, LineAlignment = PdfVerticalAlignment.Middle };

            graphics.DrawString("Invoice", new PdfStandardFont(PdfFontFamily.Helvetica, 18), PdfBrushes.Black, new RectangleF(40, 32, 515, 36), titleFormat);
            graphics.DrawLine(new PdfPen(Color.FromArgb(30, 80, 140), 2), 40, 82, 555, 82);
            graphics.DrawString("Customer", new PdfStandardFont(PdfFontFamily.Courier, 10), PdfBrushes.Blue, 40, 110);
            graphics.DrawRectangle(new PdfSolidBrush(Color.FromArgb(230, 240, 255)), 40, 145, 220, 60);
            graphics.DrawImage(PdfImage.FromFile(logoPath), 455, 105, 80, 40);
            document.Save(outputStream);
            document.Close(true);
            """;
        var expected = """
            using PXA.Pdf;
            using Syncfusion.Drawing;

            var document = new PdfDocument();
            var page = document.AddPage();
            page.DrawTextBoxFromTop("Invoice", 40, 32, 515, 36, new PdfTextBoxOptions { FontFamily = PdfFontFamily.Helvetica, FontSize = 18, FillColor = PdfColor.Black, Alignment = PdfTextAlignment.Center, VerticalAlignment = PdfVerticalAlignment.Middle });
            page.DrawLineFromTop(40, 82, 555, 82, 2, PdfColor.FromRgb(30, 80, 140));
            page.DrawTextFromTop("Customer", 40, 110, 10, PdfFontFamily.Courier);
            page.DrawRectangleFromTop(40, 145, 220, 60, 1, true, null, PdfColor.FromRgb(230, 240, 255));
            page.DrawImageFromTop(logoPath, 455, 105, 80, 40);
            document.Save(outputStream);
            """;
        var sut = new SyncfusionPdfMigration();

        var result = sut.Migrate(source);

        Assert.Equal(expected, result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC009");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC013");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGSYNC015");
    }
}
