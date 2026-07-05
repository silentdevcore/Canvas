using Canvas.Migration.Telerik;
using PXA.Core.Contracts;

namespace PXA.Migration.Report;

public sealed class TelerikReportMigration : IReportMigration
{
    private readonly TrdxToDesignConverter inner = new();

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
