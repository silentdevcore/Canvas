using Canvas.Migration.DevExpressReport;

namespace PXA.Migration.Report;

public sealed class DevExpressReportMigration : IReportMigration
{
    private readonly XtraReportToDesignConverter inner = new();

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
