using PXA.Migration.FastReport;
using PXA.Core.Contracts;

namespace PXA.Migration.Report;

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
