using PVAD.Migration.Pdf.Code.Nutrient;

namespace PVAD.WebApi.Services.Converters;

public sealed class NutrientPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Nutrient";

    public override string FrameworkName => "Nutrient .NET (formerly GdPicture.NET)";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based conversion of supported GdPicturePDF document, page, content, basic form, annotation, AES-256, and save operations. Signing, irreversible redaction, PDF OCR, scanning, and advanced editing produce explicit PVADE workflow actions.";

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
