using Canvas.Migration.PdfKitNet;

namespace Canvas.WebApi.Services.Converters;

public sealed class PdfKitNetConverter : BasePdfConverter
{
    public override string FrameworkId => "PdfKitNet";
    public override string FrameworkName => "PDFKit.NET";
    public override string Status => "pilot";
    public override string Description =>
        "Cautious Roslyn-backed pilot for likely PDFKit.NET document/page/text/shape/save patterns; package identity remains manual.";

    public override string ConvertCode(string sourceCode)
    {
        return new PdfKitNetMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new PdfKitNetMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
