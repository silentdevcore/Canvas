using PXA.Migration.PdfTools;

namespace PXA.WebApi.Services.Converters;

public sealed class PdfToolsConverter : BasePdfConverter
{
    public override string FrameworkId => "PdfTools";
    public override string FrameworkName => "PDFTools / Pdftools SDK";
    public override string Status => "pilot";
    public override string Description =>
        "Cautious Roslyn-backed pilot for PDFTools / Pdftools SDK; removes Sdk.Initialize and flags SDK conversion/processing workflows for manual Canvas.Pdf migration.";

    public override string ConvertCode(string sourceCode)
    {
        return new PdfToolsMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new PdfToolsMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
