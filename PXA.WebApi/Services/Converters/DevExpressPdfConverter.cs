using PXA.Migration.DevExpressPdf;

namespace PXA.WebApi.Services.Converters;

public sealed class DevExpressPdfConverter : BasePdfConverter
{
    public override string FrameworkId => "DevExpress";

    public override string FrameworkName => "DevExpress PDF";

    public override string Status => "full";

    public override string Description =>
        "Roslyn-based conversion: PdfDocumentProcessor → PdfDocument, CreateEmptyDocument/CreateGraphics removed, RenderNewPage → AddPage, DrawString/DrawLine/DrawRectangle repositioned after AddPage, SaveDocument → Save. Forms/signatures/report export produce warnings.";

    public override string ConvertCode(string sourceCode)
    {
        return new DevExpressPdfMigration().Migrate(sourceCode).MigratedCode;
    }

    public override IReadOnlyList<MigrationDiagnostic> GetDiagnostics(string sourceCode)
    {
        return new DevExpressPdfMigration()
            .Migrate(sourceCode)
            .Diagnostics
            .Select(static diagnostic => new MigrationDiagnostic(
                diagnostic.Id,
                diagnostic.Severity.ToString(),
                diagnostic.Message))
            .ToArray();
    }
}
