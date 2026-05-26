using Canvas.Migration.GemBoxPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class GemBoxPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "GemBox";

    public override string FrameworkName => "GemBox.Pdf";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based full conversion: PdfDocument + Pages.Add → AddPage; Content.DrawText → DrawTextFromTop; Content.DrawLine → DrawLineFromTop; Content.DrawRectangle → DrawRectangleFromTop; ComponentInfo.SetLicense removed; forms/encryption/annotations produce warnings.";

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
