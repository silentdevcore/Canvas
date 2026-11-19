using PVAD.Licensing;
using PVAD.Migration.Report.Designer.JasperReports;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

public sealed class JasperReportsMigration : IReportMigration
{
    private readonly JrxmlToDesignConverter inner = new();

    public static bool LooksLike(string source) => JrxmlToDesignConverter.LooksLikeJrxml(source);

    public ReportMigrationResult Convert(string source)
        => Convert(source, subreportSources: null);

    public ReportMigrationResult Convert(string source, IReadOnlyDictionary<string, string>? subreportSources)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ReportDesignerMigration, "jasperreports");
        return ConvertAdmitted(source, subreportSources);
    }

    internal ReportMigrationResult ConvertAdmitted(string source, IReadOnlyDictionary<string, string>? subreportSources)
    {
        PvadLicense.DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, "jasperreports");
        var result = inner.ConvertAdmitted(source, subreportSources);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
