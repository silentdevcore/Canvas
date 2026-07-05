using Canvas.Migration.JasperReports;

namespace PXA.Migration.Report;

public sealed class JasperReportsMigration : IReportMigration
{
    private readonly JrxmlToDesignConverter inner = new();

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
