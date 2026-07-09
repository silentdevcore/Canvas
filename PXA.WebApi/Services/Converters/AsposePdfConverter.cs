using Canvas.Migration.AsposePdf;

namespace PXA.WebApi.Services.Converters;

public sealed class AsposePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Aspose";

    public override string FrameworkName => "Aspose.PDF";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based conversion: Document → PdfDocument, Pages.Add → AddPage, TextFragment/TextBuilder with Position → DrawText/DrawTextFromTop. Table/forms/security produce warnings.";

    public override string ConvertCode(string sourceCode)
    {
        return new AsposePdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new AsposePdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
