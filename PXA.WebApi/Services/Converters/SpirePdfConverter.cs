using PXA.Migration.SpirePdf;

namespace PXA.WebApi.Services.Converters;

public sealed class SpirePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Spire";

    public override string FrameworkName => "Spire.PDF";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based pilot: PdfDocument + Pages.Add → AddPage; page.Canvas.DrawString/DrawLine/DrawRectangle mapped; images, tables, forms, security, and annotations require manual review.";

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
