using PXA.Migration.Report.Designer.ActiveReportsJs;
using PXA.Core.Contracts;

namespace PXA.Migration.Report;

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
