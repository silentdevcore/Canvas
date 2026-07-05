using Canvas.Migration.Stimulsoft;

namespace PXA.Migration.Report;

public sealed class StimulsoftReportMigration : IReportMigration
{
    private readonly MrtToDesignConverter inner = new();

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
