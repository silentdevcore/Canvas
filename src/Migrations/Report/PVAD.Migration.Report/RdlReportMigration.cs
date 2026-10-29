using PVAD.Migration.Report.Designer.Rdl;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class RdlReportMigration : IReportMigration
{
    private readonly RdlToDesignConverter inner = new();

    public static bool LooksLike(string source) => RdlToDesignConverter.LooksLikeRdl(source);

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
