using Canvas.Migration.ActiveReportsJs;

namespace PXA.Migration.Report;

public sealed class ActiveReportsJsMigration : IReportMigration
{
    private readonly ActiveReportsJsToDesignConverter inner = new();

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
