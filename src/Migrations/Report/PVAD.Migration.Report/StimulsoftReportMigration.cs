using PVAD.Migration.Report.Designer.Stimulsoft;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class StimulsoftReportMigration : IReportMigration
{
    private readonly MrtToDesignConverter inner = new();

    public static bool LooksLike(string source) => MrtToDesignConverter.LooksLikeMrt(source);

    public ReportMigrationResult Convert(string source)
    {
        var result = inner.ConvertAuto(source);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
