using Canvas.Migration.DevExpressReport;
using PXA.Core.Contracts;

namespace PXA.Migration.Report;

public sealed class DevExpressReportMigration : IReportMigration
{
    private readonly XtraReportToDesignConverter inner = new();

    public ReportMigrationResult Convert(string source)
    {
        var result = inner.ConvertAuto(source);
        return new ReportMigrationResult
        {
            Design = result.Design.ToPxa(),
            Diagnostics = ReportDiagnosticMapper.ToPxaDiagnostics(result.Diagnostics),
        };
    }
}
