using Canvas.Migration.GemBoxPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class GemBoxPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "GemBox";

    public override string FrameworkName => "GemBox.Pdf";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-backed pilot for GemBox.Pdf document, page, simple text, and save workflows.";

    public override string ConvertCode(string sourceCode)
    {
        return new GemBoxPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new GemBoxPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
