using PVAD.Migration.Report.Designer.Telerik;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class TelerikReportMigration : IReportMigration
{
    private readonly TrdxToDesignConverter inner = new();

    public static bool LooksLike(string source) => TrdxToDesignConverter.LooksLikeTrdx(source);

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
