using PVAD.Migration.Report.Designer.ActiveReportsJs;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class ActiveReportsJsMigration : IReportMigration
{
    private readonly ActiveReportsJsToDesignConverter inner = new();

    public static bool LooksLike(string source) => ActiveReportsJsToDesignConverter.LooksLikeActiveReportsJs(source);

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
