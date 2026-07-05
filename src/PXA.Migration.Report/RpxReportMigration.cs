using Canvas.Migration.Rpx;

namespace PXA.Migration.Report;

public sealed class RpxReportMigration : IReportMigration
{
    private readonly RpxToDesignConverter inner = new();

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
