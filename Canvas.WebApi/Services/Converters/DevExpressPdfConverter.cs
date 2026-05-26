using Canvas.Migration.DevExpressPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class DevExpressPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "DevExpress";

    public override string FrameworkName => "DevExpress PDF";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based reporting pilot for DevExpress PDF processor, drawing, and report export workflows.";

    public override string ConvertCode(string sourceCode)
    {
        return new DevExpressPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new DevExpressPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
