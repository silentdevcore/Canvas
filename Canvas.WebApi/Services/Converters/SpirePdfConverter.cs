using Canvas.Migration.SpirePdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class SpirePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Spire";

    public override string FrameworkName => "Spire.PDF";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-backed pilot for Spire.PDF document, page, canvas text/shape, and SaveToFile workflows.";

    public override string ConvertCode(string sourceCode)
    {
        return new SpirePdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new SpirePdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
