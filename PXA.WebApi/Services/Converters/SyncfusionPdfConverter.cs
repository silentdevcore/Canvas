using Canvas.Migration.SyncfusionPdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class SyncfusionPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Syncfusion";

    public override string FrameworkName => "Syncfusion PDF";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based conversion with top-left coordinate adapter. Covers document/page/text/line/rectangle/image/save and reports manual follow-up items.";

    public override string ConvertCode(string sourceCode)
    {
        return new SyncfusionPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new SyncfusionPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
