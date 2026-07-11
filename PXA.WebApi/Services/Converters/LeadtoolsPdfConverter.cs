using PXA.Migration.LeadtoolsPdf;

namespace PXA.WebApi.Services.Converters;

public sealed class LeadtoolsPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Leadtools";
    public override string FrameworkName => "LEADTOOLS";
    public override string Status => "full";
    public override string Description =>
        "Cautious Roslyn-backed pilot: likely direct PDFDocument + AddPage/Pages.Add, simple text/shape/save mappings; raster/OCR/barcode/conversion APIs require manual migration.";

    public override string ConvertCode(string sourceCode)
    {
        return new LeadtoolsPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new LeadtoolsPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
