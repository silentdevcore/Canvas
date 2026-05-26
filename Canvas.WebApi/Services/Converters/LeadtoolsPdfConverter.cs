using Canvas.Migration.LeadtoolsPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class LeadtoolsPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Leadtools";
    public override string FrameworkName => "LEADTOOLS";
    public override string Status => "pilot";
    public override string Description =>
        "Cautious Roslyn-backed pilot for likely LEADTOOLS PDF generation; raster, OCR, barcode, and conversion pipelines are manual.";

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
