using Canvas.Migration.Telerik;

namespace PXA.Migration.Report;

public sealed class TelerikReportMigration : IReportMigration
{
    private readonly TrdxToDesignConverter inner = new();

    public ReportMigrationResult Convert(string source)
    {
        var result = inner.ConvertAuto(source);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = ReportDiagnosticMapper.ToPxaDiagnostics(result.Diagnostics),
        };
    }
}
