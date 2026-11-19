using PVAD.Licensing;
using PVAD.Migration.Report.Designer.FastReport;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class FastReportMigration : IReportMigration
{
    private readonly FrxToDesignConverter inner = new();

    public static bool LooksLike(string source) => FrxToDesignConverter.LooksLikeFrx(source);

    public ReportMigrationResult Convert(string source)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ReportDesignerMigration, "fastreport");
        return ConvertAdmitted(source);
    }

    internal ReportMigrationResult ConvertAdmitted(string source)
    {
        PvadLicense.DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, "fastreport");
        var result = inner.ConvertAutoAdmitted(source);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
