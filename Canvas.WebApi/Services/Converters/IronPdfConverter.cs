using Canvas.Migration.IronPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class IronPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "IronPdf";

    public override string FrameworkName => "IronPDF";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based reporting pilot for IronPDF HTML/URL/Razor rendering flows that require manual Canvas.Pdf rewrite.";

    public override string ConvertCode(string sourceCode)
    {
        return new IronPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new IronPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
