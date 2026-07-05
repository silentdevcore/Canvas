using Canvas.Migration.Rdl;

namespace PXA.Migration.Report;

public sealed class RdlReportMigration : IReportMigration
{
    private readonly RdlToDesignConverter inner = new();

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
