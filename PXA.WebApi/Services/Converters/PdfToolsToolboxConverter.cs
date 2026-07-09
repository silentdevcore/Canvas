using Canvas.Migration.PdfToolsToolbox;

namespace PXA.WebApi.Services.Converters;

public sealed class PdfToolsToolboxConverter : BasePdfConverter
{
    public override string FrameworkId => "PdfToolsToolbox";
    public override string FrameworkName => "PDF Toolbox SDK / Toolbox add-on";
    public override string Status => "pilot";
    public override string Description =>
        "Cautious Roslyn-backed pilot for PDF Toolbox SDK direct-generation flows; maps simple Document.Create/Page.Create/TextGenerator.ShowLine patterns and warns on existing-PDF editing, styling, forms, annotations, metadata, outlines, and tagging.";

    public override string ConvertCode(string sourceCode)
    {
        return new PdfToolsToolboxMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new PdfToolsToolboxMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
