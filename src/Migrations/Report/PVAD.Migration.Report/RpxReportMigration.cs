using PVAD.Licensing;
using PVAD.Migration.Report.Designer.Rpx;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class RpxReportMigration : IReportMigration
{
    private readonly RpxToDesignConverter inner = new();

    public static bool LooksLike(string source) => RpxToDesignConverter.LooksLikeRpx(source);

    public ReportMigrationResult Convert(string source)
        => Convert(source, resources: null);

    public ReportMigrationResult Convert(string source, IReadOnlyDictionary<string, string>? resources)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ReportDesignerMigration, "rpx");
        return ConvertAdmitted(source, resources);
    }

    internal ReportMigrationResult ConvertAdmitted(string source, IReadOnlyDictionary<string, string>? resources)
    {
        PvadLicense.DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, "rpx");
        var result = inner.ConvertAdmitted(source, resources);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
