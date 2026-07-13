using PXA.Migration.Abstractions;
using PXA.Migration.Pdf.Code.PdfTools;

namespace PXA.Migration.Pdf.Code.PdfTools.Tests;

public sealed class PdfToolsMigrationTests
{
    [Fact]
    public void Migrate_ShouldRemoveSdkInitializeButKeepSdkCodeForManualMigration()
    {
        var source = """
            using PdfTools;
            using PdfTools.Pdf;

            Sdk.Initialize(licenseKey);
            using var input = File.OpenRead(inputPath);
            using var document = Document.Open(input, null);
            document.Save(outputPath);
            """;
        var sut = new PdfToolsMigration();

        var result = sut.Migrate(source);

        Assert.DoesNotContain("Sdk.Initialize", result.MigratedCode);
        Assert.Contains("using PdfTools;", result.MigratedCode);
        Assert.Contains("using PdfTools.Pdf;", result.MigratedCode);
        Assert.Contains("Document.Open(input, null);", result.MigratedCode);
        Assert.Contains("document.Save(outputPath);", result.MigratedCode);
        Assert.DoesNotContain("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLS000");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLS001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLS020");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLS021");
    }

    [Fact]
    public void Migrate_ShouldWarnForConversionOptimizationValidationAndSigning()
    {
        var source = """
            using PdfTools;

            var converter = new Converter();
            converter.ConvertToPdf(inputPath, outputPath);
            converter.Optimize(outputPath);
            converter.Validate(outputPath);
            converter.Sign(certificate);
            """;
        var sut = new PdfToolsMigration();

        var result = sut.Migrate(source);

        Assert.Contains("ConvertToPdf", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLS020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLS021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForPdfToImageRendering()
    {
        var source = """
            using PdfTools;
            using PdfTools.Image;

            using var input = File.OpenRead(inputPath);
            using var image = Document.Open(input);
            image.Render(outputPath);
            """;
        var sut = new PdfToolsMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Render(outputPath)", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLS020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForDocumentAssembly()
    {
        var source = """
            using PdfTools.DocumentAssembly;

            var assembler = new DocumentAssembler();
            assembler.Append(firstDocument);
            assembler.Assemble(outputStream);
            """;
        var sut = new PdfToolsMigration();

        var result = sut.Migrate(source);

        Assert.Contains("DocumentAssembler", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLS020"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLS021"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Migrate_ShouldWarnForToolboxDirectGenerationAsSeparateProduct()
    {
        var source = """
            using PdfTools.Toolbox.Pdf;

            using var document = Document.Create(outputStream);
            var page = Page.Create(document);
            """;
        var sut = new PdfToolsMigration();

        var result = sut.Migrate(source);

        Assert.Contains("Document.Create(outputStream)", result.MigratedCode);
        Assert.Contains("Page.Create(document)", result.MigratedCode);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGPDFTOOLS022"
            && diagnostic.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
