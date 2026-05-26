using Canvas.Migration.FoxitPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class FoxitPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Foxit";

    public override string FrameworkName => "Foxit PDF SDK";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based reporting pilot for Foxit PDFDoc, page insertion, graphics/content drawing, and save workflows.";

    public override string ConvertCode(string sourceCode)
    {
        return new FoxitPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new FoxitPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
