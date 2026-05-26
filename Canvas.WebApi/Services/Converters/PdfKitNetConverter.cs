using Canvas.Migration.PdfKitNet;

namespace Canvas.WebApi.Services.Converters;

public sealed class PdfKitNetConverter : BasePdfConverter
{
    public override string FrameworkId => "PdfKitNet";
    public override string FrameworkName => "PDFKit.NET";
    public override string Status => "full";
    public override string Description =>
        "Roslyn-based full conversion: Document + NewPage/Pages.Add → AddPage; DrawText/DrawString → DrawTextFromTop; DrawLine → DrawLineFromTop; DrawRectangle → DrawRectangleFromTop; Save/Render → Save; forms/encryption/annotations produce warnings. Package identity must be manually verified.";

    public override string ConvertCode(string sourceCode)
    {
        return new PdfKitNetMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new PdfKitNetMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
