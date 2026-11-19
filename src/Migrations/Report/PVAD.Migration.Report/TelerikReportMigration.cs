using PVAD.Licensing;
using PVAD.Migration.Report.Designer.Telerik;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class TelerikReportMigration : IReportMigration
{
    private readonly TrdxToDesignConverter inner = new();

    public static bool LooksLike(string source) => TrdxToDesignConverter.LooksLikeTrdx(source);

    public ReportMigrationResult Convert(string source)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ReportDesignerMigration, "telerik");
        return ConvertAdmitted(source);
    }

    internal ReportMigrationResult ConvertAdmitted(string source)
    {
        PvadLicense.DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, "telerik");
        var result = inner.ConvertAutoAdmitted(source);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
