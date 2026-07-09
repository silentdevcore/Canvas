using Canvas.Migration.PdfKitNet;

namespace Canvas.WebApi.Services.Converters;

public sealed class PdfKitNetConverter : BasePdfConverter
{
    public override string FrameworkId => "PdfKitNet";
    public override string FrameworkName => "PDFKit.NET";
    public override string Status => "full";
    public override string Description =>
        "Cautious Roslyn-backed pilot: likely Document + NewPage/Pages.Add → AddPage; DrawText/DrawString → DrawTextFromTop; DrawLine/DrawRectangle and Save/Render mapped; package/API identity must be manually verified.";

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
