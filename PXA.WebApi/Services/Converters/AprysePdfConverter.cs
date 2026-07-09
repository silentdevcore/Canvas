using Canvas.Migration.Apryse;

namespace PXA.WebApi.Services.Converters;

public sealed class AprysePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Apryse";

    public override string FrameworkName => "Apryse (PDFTron)";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based conversion: PDFDoc → PdfDocument, PageCreate+PagePushBack → AddPage(), doc.Save() → document.Save(). Unsupported APIs (SDF, forms, annotations, OCR) produce warnings.";

    public override string ConvertCode(string sourceCode)
    {
        return new ApryseMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new ApryseMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
