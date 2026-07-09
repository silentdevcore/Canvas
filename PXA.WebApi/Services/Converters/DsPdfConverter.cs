using Canvas.Migration.DsPdf;

namespace PXA.WebApi.Services.Converters;

public sealed class DsPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "DsPdf";

    public override string FrameworkName => "DsPdf (GrapeCity)";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based conversion: GcPdfDocument → PdfDocument; doc.NewPage() → AddPage(); page.Graphics.DrawString/DrawLine/DrawRectangle/FillRectangle → DrawTextFromTop/DrawLineFromTop/DrawRectangleFromTop; doc.Save() preserved.";

    public override string ConvertCode(string sourceCode)
    {
        return new DsPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new DsPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
