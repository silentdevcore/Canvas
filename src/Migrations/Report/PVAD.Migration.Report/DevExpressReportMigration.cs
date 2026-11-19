using PVAD.Licensing;
using PVAD.Migration.Report.Designer.DevExpress;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class DevExpressReportMigration : IReportMigration
{
    private readonly XtraReportToDesignConverter inner = new();

    public ReportMigrationResult Convert(string source)
        => Convert(source, resources: null);

    public ReportMigrationResult Convert(string source, IReadOnlyDictionary<string, string>? resources)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ReportDesignerMigration, "devexpress-report");
        return ConvertAdmitted(source, resources);
    }

    internal ReportMigrationResult ConvertAdmitted(string source, IReadOnlyDictionary<string, string>? resources)
    {
        PvadLicense.DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, "devexpress-report");
        var result = inner.ConvertAutoAdmitted(source, resources);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
