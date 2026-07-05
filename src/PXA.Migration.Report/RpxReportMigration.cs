using Canvas.Migration.Rpx;
using PXA.Core.Contracts;

namespace PXA.Migration.Report;

public sealed class RpxReportMigration : IReportMigration
{
    private readonly RpxToDesignConverter inner = new();

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
