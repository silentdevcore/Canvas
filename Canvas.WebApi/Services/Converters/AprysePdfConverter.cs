using Canvas.Migration.Apryse;

namespace Canvas.WebApi.Services.Converters;

public sealed class AprysePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Apryse";

    public override string FrameworkName => "Apryse (PDFTron)";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based reporting pilot for Apryse/PDFNet document, page, ElementBuilder, ElementWriter, and save workflows.";

    public override string ConvertCode(string sourceCode)
    {
        return new ApryseMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new ApryseMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
