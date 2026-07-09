using Canvas.Migration.FoxitPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class FoxitPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Foxit";

    public override string FrameworkName => "Foxit PDF SDK";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based conversion: PDFDoc → PdfDocument; InsertPage/CreatePage → AddPage; Library.Initialize + GetGraphics/GenerateContent removed; graphics.DrawText/DrawLine/DrawRect/FillRect → DrawTextFromTop/DrawLineFromTop/DrawRectangleFromTop; doc.Save/SaveAs → document.Save().";

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
