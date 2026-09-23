using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PXA.Migration.Abstractions;
using PXA.Migration.Pdf.Code.Nutrient;
using PXA.Pdf;

namespace PXA.Migration.Pdf.Code.Nutrient.Tests;

public sealed class NutrientMigrationTests
{
    [Fact]
    public void Migrate_ConvertsOfficialStyleGenerationLifecycle()
    {
        var source = """
            using GdPicture14;

            using GdPicturePDF gdpicturePDF = new GdPicturePDF();
            gdpicturePDF.SetMeasurementUnit(PdfMeasurementUnit.PdfMeasurementUnitPoint);
            gdpicturePDF.SetOrigin(PdfOrigin.PdfOriginTopLeft);
            gdpicturePDF.NewPDF();
            gdpicturePDF.NewPage(PdfPageSizes.PdfPageSizeA4);
            var fontHelvetica = gdpicturePDF.AddStandardFont(PdfStandardFont.PdfStandardFontHelvetica);
            gdpicturePDF.SetTextSize(30);
            gdpicturePDF.DrawText(fontHelvetica, 15, 40, "Name");
            gdpicturePDF.SaveToFile(outputPath);
            gdpicturePDF.Dispose();
            """;

        var result = new NutrientMigration().Migrate(source);

        Assert.Contains("using PXA.Pdf;", result.MigratedCode);
        Assert.DoesNotContain("GdPicture14", result.MigratedCode);
        Assert.Contains("var gdpicturePDF = new PdfDocument();", result.MigratedCode);
        Assert.Contains("var page = gdpicturePDF.AddPage(PdfPagePreset.A4);", result.MigratedCode);
        Assert.Contains("var fontHelvetica = PdfStandardFont.Helvetica;", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Name\", 15, 40", result.MigratedCode);
        Assert.Contains("FontSize = 30", result.MigratedCode);
        Assert.Contains("gdpicturePDF.Save(outputPath);", result.MigratedCode);
        Assert.DoesNotContain("NewPDF", result.MigratedCode);
        Assert.DoesNotContain("Dispose", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGNUTNET001");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGNUTNET011");
    }

    [Fact]
    public void Migrate_NormalizesMillimetersToPdfPoints()
    {
        var source = """
            using GdPicture14;

            var pdf = new GdPicturePDF();
            pdf.NewPDF();
            pdf.NewPage(PdfPageSizes.PdfPageSizeA4);
            pdf.SetOrigin(PdfOrigin.PdfOriginTopLeft);
            pdf.SetMeasurementUnit(PdfMeasurementUnit.PdfMeasurementUnitMillimeter);
            var font = pdf.AddStandardFont(PdfStandardFont.PdfStandardFontHelveticaBold);
            pdf.SetTextSize(10);
            pdf.DrawText(font, 10, 20, "Invoice");
            pdf.SaveToFile("invoice.pdf");
            """;

        var result = new NutrientMigration().Migrate(source);

        Assert.Contains("PdfStandardFont.HelveticaBold", result.MigratedCode);
        Assert.Contains("28.3465", result.MigratedCode);
        Assert.Contains("56.6929", result.MigratedCode);
        Assert.Contains("FontSize = 28.3465", result.MigratedCode);
    }

    [Fact]
    public void Migrate_UsesBottomLeftDrawTextByDefault()
    {
        var source = """
            using GdPicture14;
            var pdf = new GdPicturePDF();
            pdf.NewPDF();
            pdf.NewPage(500, 500);
            var font = pdf.AddStandardFont(PdfStandardFont.PdfStandardFontCourier);
            pdf.DrawText(font, 200, 250, "Hello World");
            pdf.SaveToFile(path);
            """;

        var result = new NutrientMigration().Migrate(source);

        Assert.Contains("var page = pdf.AddPage(500, 500);", result.MigratedCode);
        Assert.Contains("page.DrawText(\"Hello World\", 200, 250", result.MigratedCode);
        Assert.Contains("PdfStandardFont.Courier", result.MigratedCode);
    }

    [Fact]
    public void Migrate_WarnsWithoutRemovingTrustOperations()
    {
        var source = """
            using GdPicture14;
            var pdf = new GdPicturePDF();
            pdf.LoadFromFile(inputPath);
            pdf.AddRedactionRegion(10, 10, 20, 20);
            pdf.ApplyRedaction();
            pdf.SignDocument(certificate);
            pdf.SaveToFile(outputPath);
            """;

        var result = new NutrientMigration().Migrate(source);

        Assert.Contains("LoadFromFile", result.MigratedCode);
        Assert.Contains("ApplyRedaction", result.MigratedCode);
        Assert.Contains("SignDocument", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGNUTNET020" && d.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGNUTNET021");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGNUTNET022");
        Assert.NotNull(result.PdfTrust);
        Assert.Equal(PdfTrustMigrationDisposition.ReviewRequired, result.PdfTrust.OverallDisposition);
        Assert.Contains(result.PdfTrust.Evidence, item =>
            item.Capability == PdfTrustCapability.DigitalSignature &&
            item.SourceOperation.Contains("SignDocument", StringComparison.Ordinal));
        var redaction = Assert.Single(result.PdfTrust.Evidence, item =>
            item.Capability == PdfTrustCapability.SecureRedaction);
        Assert.Equal(PdfTrustSourceSemantics.ContentRemovingRedaction, redaction.SourceSemantics);
        Assert.Equal("false", redaction.ProviderOptions["automaticReplacement"]);
        Assert.StartsWith("/docs/", redaction.DocumentationPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Migrate_WarnsWhenPdfAConformanceWouldBeLost()
    {
        var source = """
            using GdPicture14;
            var pdf = new GdPicturePDF();
            pdf.NewPDF(PdfConformance.PDF_A_1a);
            pdf.NewPage(PdfPageSizes.PdfPageSizeA4);
            pdf.SaveToFile(outputPath);
            """;

        var result = new NutrientMigration().Migrate(source);

        Assert.DoesNotContain("NewPDF", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGNUTNET006" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_MapsPdfA2bSaveOptions()
    {
        var source = """
            using GdPicture14;
            var pdf = new GdPicturePDF();
            pdf.NewPDF(PdfConformance.PDF_A_2b);
            pdf.NewPage(PdfPageSizes.PdfPageSizeA4);
            pdf.SaveToFile(outputPath);
            """;

        var result = new NutrientMigration().Migrate(source);

        Assert.Contains("ComplianceProfile = PdfComplianceProfile.PdfA2B", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGNUTNET032");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CANMIGNUTNET023");
        var pdfA = Assert.Single(result.PdfTrust!.Evidence, item => item.Capability == PdfTrustCapability.PdfA);
        Assert.Equal(PdfTrustMigrationDisposition.ReviewRequired, pdfA.Disposition);
        Assert.Equal("PDF/A-2b", pdfA.ProviderOptions["profile"]);
    }

    [Fact]
    public void Migrate_ProducesCompilableCodeForSupportedFixture()
    {
        var source = """
            using GdPicture14;
            var pdf = new GdPicturePDF();
            pdf.NewPDF();
            pdf.NewPage(PdfPageSizes.PdfPageSizeLetter);
            var font = pdf.AddStandardFont(PdfStandardFont.PdfStandardFontHelvetica);
            pdf.SetOrigin(PdfOrigin.PdfOriginTopLeft);
            pdf.DrawText(font, 40, 50, "Migrated");
            pdf.SaveToFile("output.pdf");
            """;
        var migrated = new NutrientMigration().Migrate(source).MigratedCode;
        var syntaxTree = CSharpSyntaxTree.ParseText(migrated);

        var errors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        Assert.Empty(errors);
        Assert.Contains("PdfPagePreset.Letter", migrated);
    }

    [Fact]
    public void ComprehensiveFixture_MapsEncryptionAndProducesActionableTrustReview()
    {
        var result = new NutrientMigration().Migrate(NutrientCodeExamples.ComprehensiveGdPictureCSharp);

        Assert.DoesNotContain("SYNTHETIC-LICENSE", result.MigratedCode);
        Assert.DoesNotContain("certificate.pfx", result.MigratedCode);
        Assert.DoesNotContain("TODO", result.MigratedCode);
        Assert.Contains("[CANMIGNUTNET058] ApplyRedaction", result.MigratedCode);
        Assert.Contains("[CANMIGNUTNET059] SignPDF", result.MigratedCode);
        Assert.Contains("PdfEncryptionAlgorithm.Aes256", result.MigratedCode);
        Assert.Contains("PXA_PDF_OWNER_PASSWORD", result.MigratedCode);
        Assert.Contains("PXA_PDF_USER_PASSWORD", result.MigratedCode);
        Assert.DoesNotContain("owner-password", result.MigratedCode);
        Assert.DoesNotContain("user-password", result.MigratedCode);
        Assert.DoesNotContain("secret", result.MigratedCode);
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET026");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET021");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET022");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET024");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET055");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET056");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET057");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET058");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET059");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET060");
        Assert.DoesNotContain(result.Diagnostics, item => item.Id == "CANMIGNUTNET025");
        Assert.Contains("page.DrawRectangleFromTop(48, 90, 500, 120", result.MigratedCode);
        Assert.Contains("var logo = \"invoice-logo.jpg\";", result.MigratedCode);
        Assert.Contains("page.DrawImageFromTop(logo, 460, 36, 84, 42);", result.MigratedCode);
        Assert.Contains("page.AddWebLink(48, page.Height - (240) - (20), 180, 20", result.MigratedCode);
        Assert.Contains("page2.RotationDegrees = (int)(90);", result.MigratedCode);
        Assert.Contains("page2.AddTextField(\"customerName\", 48, page2.Height - (72) - (28), 240, 28, \"Example Customer\", 10);", result.MigratedCode);
        Assert.Contains("page2.AddCheckBox(\"approved\", 48, page2.Height - (120) - (18), 18, true);", result.MigratedCode);
        Assert.Contains("page2.AddStickyNoteAnnotation(320, page2.Height - (72) - (18), 18, 18, \"Review this page\"", result.MigratedCode);
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET016");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET017");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET018");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET019");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET027");
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET028");

        var syntaxTree = CSharpSyntaxTree.ParseText(result.MigratedCode);
        Assert.DoesNotContain(syntaxTree.GetDiagnostics(), item => item.Severity == DiagnosticSeverity.Error);
        AssertCompiles(result.MigratedCode);
    }

    [Fact]
    public void Migrate_MapsNutrientEncryptionToAes256WithoutCopyingLiteralSecrets()
    {
        var source = """
            using GdPicture14;
            var pdf = new GdPicturePDF();
            pdf.NewPDF();
            pdf.NewPage(PdfPageSizes.PdfPageSizeA4);
            pdf.Encrypt("owner-secret", "user-secret");
            pdf.SaveToFile("encrypted.pdf");
            """;

        var result = new NutrientMigration().Migrate(source);

        Assert.Contains("Encryption = new PdfEncryptionOptions", result.MigratedCode);
        Assert.Contains("Algorithm = PdfEncryptionAlgorithm.Aes256", result.MigratedCode);
        Assert.Contains("Environment.GetEnvironmentVariable(\"PXA_PDF_OWNER_PASSWORD\")", result.MigratedCode);
        Assert.Contains("Environment.GetEnvironmentVariable(\"PXA_PDF_USER_PASSWORD\")", result.MigratedCode);
        Assert.DoesNotContain("owner-secret", result.MigratedCode);
        Assert.DoesNotContain("user-secret", result.MigratedCode);
        Assert.DoesNotContain(result.Diagnostics, item => item.Id == "CANMIGNUTNET023");
        AssertCompiles(result.MigratedCode);
    }

    [Fact]
    public void Migrate_MapsOfficialTextBoxLineAndStaticPageSelection()
    {
        var source = """
            using GdPicture14;
            var pdf = new GdPicturePDF();
            pdf.NewPDF();
            pdf.SetOrigin(PdfOrigin.PdfOriginTopLeft);
            pdf.NewPage(PdfPageSizes.PdfPageSizeA4);
            var font = pdf.AddStandardFont(PdfStandardFont.PdfStandardFontHelvetica);
            pdf.SetTextSize(11);
            pdf.SetFillColor(12, 34, 56);
            pdf.SetLineColor(70, 80, 90);
            pdf.SetLineWidth(2);
            pdf.DrawTextBox(font, 40, 60, 240, 80, TextAlignment.TextAlignmentFar, TextAlignment.TextAlignmentFar, "Wrapped text");
            pdf.DrawLine(40, 150, 280, 150);
            pdf.NewPage(PdfPageSizes.PdfPageSizeLetter);
            pdf.SelectPage(1);
            pdf.DrawText(font, 40, 200, "Back on page one");
            pdf.SaveToFile("mapped.pdf");
            """;

        var result = new NutrientMigration().Migrate(source);

        Assert.Contains("page.DrawTextBoxFromTop(\"Wrapped text\", 40, 60, 240, 80", result.MigratedCode);
        Assert.Contains("FillColor = PdfColor.FromRgb(12, 34, 56)", result.MigratedCode);
        Assert.Contains("Alignment = PdfTextAlignment.Right", result.MigratedCode);
        Assert.Contains("VerticalAlignment = PdfVerticalAlignment.Bottom", result.MigratedCode);
        Assert.Contains("page.DrawLineFromTop(40, 150, 280, 150, 2, PdfColor.FromRgb(70, 80, 90));", result.MigratedCode);
        Assert.Contains("page.DrawTextFromTop(\"Back on page one\"", result.MigratedCode);
        Assert.DoesNotContain("Migrate Nutrient SelectPage", result.MigratedCode);
        AssertCompiles(result.MigratedCode);
    }

    [Fact]
    public void Migrate_DoesNotGuessGeometryForRightSideOrigins()
    {
        var source = """
            using GdPicture14;
            var pdf = new GdPicturePDF();
            pdf.NewPDF();
            pdf.NewPage(PdfPageSizes.PdfPageSizeA4);
            var font = pdf.AddStandardFont(PdfStandardFont.PdfStandardFontHelvetica);
            pdf.SetOrigin(PdfOrigin.PdfOriginTopRight);
            pdf.DrawText(font, 20, 30, "Right origin");
            pdf.SaveToFile("right-origin.pdf");
            """;

        var result = new NutrientMigration().Migrate(source);

        Assert.Contains("[CANMIGNUTNET049] DrawText", result.MigratedCode);
        Assert.DoesNotContain("page.DrawText", result.MigratedCode);
        Assert.Contains(result.Diagnostics, item => item.Id == "CANMIGNUTNET049");
        AssertCompiles(result.MigratedCode);
    }

    private static void AssertCompiles(string source)
    {
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();
        trustedAssemblies.Add(MetadataReference.CreateFromFile(typeof(PdfDocument).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            $"NutrientMigration_{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source)],
            trustedAssemblies,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        var errors = compilation.GetDiagnostics()
            .Where(item => item.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(item => item.ToString())));
    }
}
