using Canvas.Migration.LeadtoolsPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class LeadtoolsPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Leadtools";
    public override string FrameworkName => "LEADTOOLS";
    public override string Status => "full";
    public override string Description =>
        "Roslyn-based full conversion: PDFDocument + AddPage/Pages.Add → AddPage; DrawText/DrawString → DrawTextFromTop; DrawLine → DrawLineFromTop; DrawRectangle → DrawRectangleFromTop; Save/Export → Save; raster/OCR/barcode/conversion APIs produce warnings.";

    public override string ConvertCode(string sourceCode)
    {
        return new LeadtoolsPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new LeadtoolsPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
