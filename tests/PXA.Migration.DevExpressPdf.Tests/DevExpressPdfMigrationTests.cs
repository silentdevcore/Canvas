using PXA.Migration.Abstractions;
using PXA.Migration.DevExpressPdf;

namespace PXA.Migration.DevExpressPdf.Tests;

public sealed class DevExpressPdfMigrationTests
{
    [Fact]
    public void Migrate_BasicGenerationWorkflow_ProducesCanvasCode()
    {
        var source = """
            using DevExpress.Pdf;
            using DevExpress.Drawing;

            using var processor = new PdfDocumentProcessor();
            processor.CreateEmptyDocument();
            using var graphics = processor.CreateGraphics();
            graphics.DrawString("Hello", new DXFont("Arial", 12), DXBrushes.Black, 40, 40);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains("var document = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Hello\", 40, 40, 12);", result.MigratedCode);
        Assert.Contains("document.Save(path);", result.MigratedCode);
        Assert.DoesNotContain("using DevExpress", result.MigratedCode);
        Assert.DoesNotContain("PdfDocumentProcessor", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP002");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP003");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP004");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP005");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP008");
    }

    [Fact]
    public void Migrate_LineAndRectangleDrawing_ProducesCanvasDrawCalls()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            processor.CreateEmptyDocument();
            using var graphics = processor.CreateGraphics();
            graphics.DrawLine(pen, 40, 700, 555, 700);
            graphics.DrawRectangle(pen, 40, 620, 200, 80);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(outputPath);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLine(40, 700, 555, 700);", result.MigratedCode);
        Assert.Contains("page.DrawRectangle(40, 620, 200, 80);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP006");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP007");
    }

    [Fact]
    public void Migrate_DrawCallsRepositionedAfterAddPage()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            processor.CreateEmptyDocument();
            using var graphics = processor.CreateGraphics();
            graphics.DrawString("Title", new DXFont("Arial", 18), DXBrushes.Black, 40, 750);
            graphics.DrawString("Body", new DXFont("Arial", 12), DXBrushes.Black, 40, 700);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        var addPageIndex = result.MigratedCode.IndexOf("document.AddPage()", StringComparison.Ordinal);
        var drawTitleIndex = result.MigratedCode.IndexOf("DrawTextFromTop(\"Title\"", StringComparison.Ordinal);
        var drawBodyIndex = result.MigratedCode.IndexOf("DrawTextFromTop(\"Body\"", StringComparison.Ordinal);

        Assert.True(addPageIndex >= 0, "AddPage() not found");
        Assert.True(drawTitleIndex > addPageIndex, "Title draw call should come after AddPage");
        Assert.True(drawBodyIndex > addPageIndex, "Body draw call should come after AddPage");
        Assert.Contains("page.DrawTextFromTop(\"Title\", 40, 750, 18);", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Body\", 40, 700, 12);", result.MigratedCode);
    }

    [Theory]
    [InlineData("PdfPaperSize.A4", "var page = document.AddPage();")]
    [InlineData("PdfPaperSize.A3", "var page = document.AddPage(PdfPagePreset.A3);")]
    [InlineData("PdfPaperSize.Letter", "var page = document.AddPage(PdfPagePreset.Letter);")]
    [InlineData("PdfPaperSize.Legal", "var page = document.AddPage(612, 1008);")]
    [InlineData("PdfPaperSize.A5", "var page = document.AddPage(420, 595);")]
    public void Migrate_PaperSize_MapsToCanvasAddPage(string paperSize, string expectedAddPage)
    {
        var source = $$"""
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            processor.RenderNewPage({{paperSize}}, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(expectedAddPage, result.MigratedCode);
    }

    [Fact]
    public void Migrate_ExplicitPageDimensions_MapToAddPageWidthHeight()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            processor.RenderNewPage(400, 600, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var page = document.AddPage(400, 600);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_UnmappedPaperSize_DefaultsToA4WithWarning()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            processor.RenderNewPage(PdfPaperSize.Tabloid, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("var page = document.AddPage();", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP026" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ExistingPdfProcessing_EmitsWarning()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            processor.LoadDocument(inputPath);
            processor.DeletePage(1);
            processor.SaveDocument(outputPath);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP021" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_FormsAndSignatures_EmitsWarning()
    {
        var source = """
            using DevExpress.Pdf;

            var signer = new PdfDocumentSigner(stream);
            var field = new PdfFormField();
            var options = new PdfEncryptionOptions();
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP022" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_MultiplePages_ReusesPageVariable()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            processor.CreateEmptyDocument();
            using var graphics = processor.CreateGraphics();
            graphics.DrawString("Page1", new DXFont("Arial", 12), DXBrushes.Black, 40, 40);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            graphics.DrawString("Page2", new DXFont("Arial", 12), DXBrushes.Black, 40, 40);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        // Exactly one declaration, then a plain reassignment for the second page.
        var declCount = result.MigratedCode.Split("var page =").Length - 1;
        Assert.Equal(1, declCount);
        Assert.Contains("page = document.AddPage();", result.MigratedCode);

        // Each page's draw call follows its own AddPage in order.
        var firstAddPage = result.MigratedCode.IndexOf("document.AddPage();", StringComparison.Ordinal);
        var page1Draw = result.MigratedCode.IndexOf("DrawTextFromTop(\"Page1\"", StringComparison.Ordinal);
        var secondAddPage = result.MigratedCode.IndexOf("document.AddPage();", page1Draw, StringComparison.Ordinal);
        var page2Draw = result.MigratedCode.IndexOf("DrawTextFromTop(\"Page2\"", StringComparison.Ordinal);
        Assert.True(page1Draw > firstAddPage && page1Draw < secondAddPage, "Page1 draw should sit on first page");
        Assert.True(page2Draw > secondAddPage, "Page2 draw should sit on second page");
    }

    [Fact]
    public void Migrate_NamedPenColour_MapsToCanvasColour()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            graphics.DrawLine(DXPens.Red, 40, 700, 555, 700);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLine(40, 700, 555, 700, 1, PdfColor.RedColor);", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP009");
    }

    [Fact]
    public void Migrate_DefaultBlackColour_KeepsShortForm()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            graphics.DrawLine(DXPens.Black, 40, 700, 555, 700);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawLine(40, 700, 555, 700);", result.MigratedCode);
        Assert.DoesNotContain("PdfColor", result.MigratedCode);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CANMIGDEVEXP009");
    }

    [Fact]
    public void Migrate_FromArgbColour_MapsToFromRgb()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            graphics.DrawRectangle(new DXPen(DXColor.FromArgb(10, 20, 30), 2), 40, 620, 200, 80);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawRectangle(40, 620, 200, 80, 2, false, PdfColor.FromRgb(10, 20, 30));", result.MigratedCode);
    }

    [Fact]
    public void Migrate_BrushOnDrawString_MapsToTextOptions()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            graphics.DrawString("Hi", new DXFont("Arial", 18), DXBrushes.Blue, 40, 40);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("FontSize = 18", result.MigratedCode);
        Assert.Contains("FillColor = PdfColor.BlueColor", result.MigratedCode);
    }

    [Fact]
    public void Migrate_InlineRectangleF_DecomposesBounds()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            graphics.DrawRectangle(pen, new RectangleF(40, 620, 200, 80));
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawRectangle(40, 620, 200, 80);", result.MigratedCode);
    }

    [Fact]
    public void Migrate_VariableRectangleF_EmitsWarning()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            graphics.DrawRectangle(pen, bounds);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP023" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_FontPassedAsVariable_RecoversFontSize()
    {
        var source = """
            using DevExpress.Pdf;

            using var processor = new PdfDocumentProcessor();
            using var graphics = processor.CreateGraphics();
            var titleFont = new DXFont("Arial", 24);
            graphics.DrawString("Title", titleFont, DXBrushes.Black, 40, 40);
            processor.RenderNewPage(PdfPaperSize.A4, graphics);
            processor.SaveDocument(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains("page.DrawTextFromTop(\"Title\", 40, 40, 24);", result.MigratedCode);
    }

    // Mirrors the "complex and complete" DevExpress sample offered in the Migrations UI
    // (ui-designer-v2 MigrationsPage DEVEXPRESS_EXAMPLE). Exercises every V2 capability at once:
    // font-as-variable, named colours, DXColor.FromArgb, inline RectangleF, multi-page, encryption.
    [Fact]
    public void Migrate_ComplexUiExample_ConvertsAllV2Features()
    {
        var source = """
            using DevExpress.Pdf;
            using DevExpress.Drawing;

            using var processor = new PdfDocumentProcessor();
            processor.CreateEmptyDocument();
            using var graphics = processor.CreateGraphics();

            var titleFont = new DXFont("Arial", 24);
            var labelFont = new DXFont("Arial", 12);

            graphics.DrawString("ACME Corporation", titleFont, DXBrushes.Black, 40, 760);
            graphics.DrawString("Annual Invoice 2024", labelFont, DXBrushes.Blue, 40, 730);
            graphics.DrawLine(new DXPen(DXColor.FromArgb(0, 102, 204), 2), 40, 715, 555, 715);
            graphics.DrawString("Prepared for: Wile E. Coyote", labelFont, DXBrushes.Black, 40, 690);
            graphics.DrawRectangle(DXPens.Red, 40, 600, 250, 70);
            graphics.DrawRectangle(DXPens.Black, new RectangleF(320, 600, 200, 70));

            processor.RenderNewPage(PdfPaperSize.A4, graphics);

            graphics.DrawString("Line Items", titleFont, DXBrushes.Black, 40, 760);
            graphics.DrawLine(DXPens.Gray, 40, 740, 555, 740);
            graphics.DrawString("Total Due", titleFont, DXBrushes.Green, 40, 660);

            processor.RenderNewPage(PdfPaperSize.A4, graphics);

            var encryptionOptions = new PdfEncryptionOptions();
            encryptionOptions.UserPasswordString = "open-sesame";
            encryptionOptions.OwnerPasswordString = "admin";
            var saveOptions = new PdfSaveOptions { EncryptionOptions = encryptionOptions };
            processor.SaveDocument(outputPath, saveOptions);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);
        var code = result.MigratedCode;

        // Multi-page: one declaration + one reassignment.
        Assert.Equal(1, code.Split("var page =").Length - 1);
        Assert.Contains("page = document.AddPage();", code);

        // Font size recovered from the variable declarations.
        Assert.Contains("page.DrawTextFromTop(\"ACME Corporation\", 40, 760, 24);", code);

        // Named brush on text → PdfDrawTextOptions.
        Assert.Contains("FillColor = PdfColor.BlueColor", code);
        Assert.Contains("FillColor = PdfColor.GreenColor", code);

        // DXColor.FromArgb pen on a line, with width.
        Assert.Contains("page.DrawLine(40, 715, 555, 715, 2, PdfColor.FromRgb(0, 102, 204));", code);

        // Named pen on a rectangle + inline RectangleF decomposed.
        Assert.Contains("page.DrawRectangle(40, 600, 250, 70, 1, false, PdfColor.RedColor);", code);
        Assert.Contains("page.DrawRectangle(320, 600, 200, 70);", code);

        // Fix 1: the DXFont declarations are removed (no leftover vendor type that won't compile).
        Assert.DoesNotContain("DXFont", code);

        // Fix 2: encryption is actually applied — Save carries mapped Canvas options, not dropped.
        Assert.Contains("document.Save(outputPath, new PdfSaveOptions", code);
        Assert.Contains("Encryption = new PdfEncryptionOptions", code);
        Assert.Contains("UserPassword = \"open-sesame\"", code);
        Assert.Contains("OwnerPassword = \"admin\"", code);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVEXP010");

        // The broken DevExpress-named encryption members are gone (would not compile under PXA.Pdf).
        Assert.DoesNotContain("UserPasswordString", code);
        Assert.DoesNotContain("OwnerPasswordString", code);
        // The bare `document.Save(outputPath);` (options dropped) must NOT appear.
        Assert.DoesNotContain("document.Save(outputPath);", code);

        // No DevExpress remnants in the converted document/draw calls.
        Assert.DoesNotContain("graphics.", code);
        Assert.DoesNotContain("RenderNewPage", code);
    }

    [Fact]
    public void Migrate_Encryption_EmitsDedicatedGuidanceDiagnostic()
    {
        var source = """
            using DevExpress.Pdf;

            var options = new PdfEncryptionOptions();
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        // Encryption now has its own actionable diagnostic, not the generic forms/signatures warning.
        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP024" && d.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CANMIGDEVEXP022");
    }

    [Fact]
    public void Migrate_ReportExportWorkflow_EmitsWarning()
    {
        var source = """
            using DevExpress.XtraReports.UI;

            var report = new XtraReport();
            report.ExportToPdf(path);
            """;
        var sut = new DevExpressPdfMigration();

        var result = sut.Migrate(source);

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGDEVEXP020" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
