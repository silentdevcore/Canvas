using PXA.Migration.Pdf.Code.IronPdf;

namespace PXA.WebApi.Services.Converters;

public sealed class IronPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "IronPdf";

    public override string FrameworkName => "IronPDF";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based pilot: ChromePdfRenderer/HtmlToPdf → PdfDocument + AddPage scaffold; SaveAs → document.Save(); HTML/URL/Razor rendering calls replaced with diagnostics for manual PXA draw call migration.";

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
