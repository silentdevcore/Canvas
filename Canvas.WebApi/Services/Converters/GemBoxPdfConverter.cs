using Canvas.Migration.GemBoxPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class GemBoxPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "GemBox";

    public override string FrameworkName => "GemBox.Pdf";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based pilot: PdfDocument + Pages.Add → AddPage; simple Content.DrawText/save mappings and license removal; complex content, forms, security, and annotations require manual review.";

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
