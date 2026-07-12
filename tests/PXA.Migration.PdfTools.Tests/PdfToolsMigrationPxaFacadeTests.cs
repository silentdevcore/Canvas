using PXA.Migration.Abstractions;

namespace PXA.Migration.PdfTools.Tests;

public sealed class PdfToolsMigrationPxaFacadeTests
{
    [Fact]
    public void Migrate_RemovesSdkInitializeButKeepsSdkCodeForManualMigration()
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

        MigrationResult result = sut.Migrate(source);

        Assert.DoesNotContain("Sdk.Initialize", result.MigratedCode);
        Assert.Contains("using PdfTools;", result.MigratedCode);
        Assert.Contains("Document.Open(input, null);", result.MigratedCode);
        Assert.DoesNotContain("using PXA.Pdf;", result.MigratedCode);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGPDFTOOLS000");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGPDFTOOLS001");
    }

    [Fact]
    public void Migrate_MapsCanvasWarningsToPxaDiagnostics()
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

        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGPDFTOOLS020" && d.Severity == MigrationDiagnosticSeverity.Warning);
        Assert.Contains(result.Diagnostics, d =>
            d.Id == "CANMIGPDFTOOLS021" && d.Severity == MigrationDiagnosticSeverity.Warning);
    }
}
