using Canvas.Migration.DsPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class DsPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "DsPdf";

    public override string FrameworkName => "DsPdf (GrapeCity)";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based reporting pilot for DsPdf/GcPdf document, page, graphics drawing, and save workflows.";

    public override string ConvertCode(string sourceCode)
    {
        return new DsPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new DsPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
