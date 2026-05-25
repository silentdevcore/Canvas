using Canvas.Migration.iText7;

namespace Canvas.WebApi.Services.Converters;

public sealed class IText7PdfConverter : BasePdfConverter
{
    public override string FrameworkId => "iText7";

    public override string FrameworkName => "iText7";

    public override string Status => "pilot";

    public override string Description =>
        "Roslyn-based pilot conversion for PdfWriter + PdfDocument + Document + simple Paragraph flows.";

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
