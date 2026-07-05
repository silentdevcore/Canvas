using Canvas.Migration.FastReport;

namespace PXA.Migration.Report;

public sealed class FastReportMigration : IReportMigration
{
    private readonly FrxToDesignConverter inner = new();

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
