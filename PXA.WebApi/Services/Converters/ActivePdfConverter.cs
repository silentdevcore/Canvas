using Canvas.Migration.ActivePdf;

namespace PXA.WebApi.Services.Converters;

public sealed class ActivePdfConverter : BasePdfConverter
{
    public override string FrameworkId => "ActivePdf";
    public override string FrameworkName => "ActivePDF";
    public override string Status => "pilot";
    public override string Description =>
        "Cautious Roslyn-backed pilot for likely ActivePDF Toolkit-style generation; DocConverter, WebGrabber, COM/server, printer, merge, and stamp workflows are manual.";

    public override string ConvertCode(string sourceCode)
    {
        return new ActivePdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new ActivePdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
