using PVAD.Migration.Report.Designer.FastReport;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class FastReportMigration : IReportMigration
{
    private readonly FrxToDesignConverter inner = new();

    public static bool LooksLike(string source) => FrxToDesignConverter.LooksLikeFrx(source);

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
