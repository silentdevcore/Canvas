using PXA.Migration.Pdf.Code.Nutrient;

namespace PXA.WebApi.Services.Converters;

public sealed class NutrientPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Nutrient";

    public override string FrameworkName => "Nutrient .NET (formerly GdPicture.NET)";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based conversion of supported GdPicturePDF document, page, text, unit, font, and save operations. Trust, redaction, OCR, forms, and advanced editing operations produce explicit review diagnostics.";

    public override string ConvertCode(string sourceCode) =>
        new NutrientMigration().Migrate(sourceCode).MigratedCode;

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode) =>
        new NutrientMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
}
