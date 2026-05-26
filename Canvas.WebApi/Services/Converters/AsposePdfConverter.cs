using Canvas.Migration.AsposePdf;

namespace Canvas.WebApi.Services.Converters;

public sealed class AsposePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "Aspose";

    public override string FrameworkName => "Aspose.PDF";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based pilot conversion for Document + Pages.Add + simple TextFragment/TextBuilder flows.";

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
