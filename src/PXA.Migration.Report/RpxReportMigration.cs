using PXA.Migration.Rpx;
using PXA.Core.Contracts;

namespace PXA.Migration.Report;

public sealed class RpxReportMigration : IReportMigration
{
    private readonly RpxToDesignConverter inner = new();

    public static bool LooksLike(string source) => RpxToDesignConverter.LooksLikeRpx(source);

    public ReportMigrationResult Convert(string source)
        => Convert(source, resources: null);

    public ReportMigrationResult Convert(string source, IReadOnlyDictionary<string, string>? resources)
    {
        var result = inner.Convert(source, resources);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
