using PVAD.Licensing;
using PVAD.Migration.Report.Designer.ActiveReportsJs;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class ActiveReportsJsMigration : IReportMigration
{
    private readonly ActiveReportsJsToDesignConverter inner = new();

    public static bool LooksLike(string source) => ActiveReportsJsToDesignConverter.LooksLikeActiveReportsJs(source);

    public ReportMigrationResult Convert(string source)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ReportDesignerMigration, "activereports-js");
        return ConvertAdmitted(source);
    }

    internal ReportMigrationResult ConvertAdmitted(string source)
    {
        PvadLicense.DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, "activereports-js");
        var result = inner.ConvertAutoAdmitted(source);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
