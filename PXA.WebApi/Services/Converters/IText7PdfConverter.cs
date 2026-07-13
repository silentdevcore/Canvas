using PXA.Migration.Pdf.Code.IText7;

namespace PXA.WebApi.Services.Converters;

public sealed class IText7PdfConverter : BasePdfConverter
{
    public override string FrameworkId => "iText7";

    public override string FrameworkName => "iText7";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based conversion: PdfWriter+PdfDocument+Document → PdfDocument; Paragraph (with SetFontSize) → DrawTextFromTop; ShowTextAligned → DrawText; PdfCanvas line/rect/text → Draw*; document.Close/SetMargins removed.";

    public override string ConvertCode(string sourceCode)
    {
        return new IText7Migration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new IText7Migration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
