using PVAD.Licensing;
using PVAD.Migration.Report.Designer.DevExpress;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

/// <summary>Imports DevExpress report definitions or supported Designer source as editable PVAD designs.</summary>
/// <remarks>Requires Report Designer Migration rights for <c>devexpress-report</c>, not PDF Code Migration rights.</remarks>
public sealed class DevExpressReportMigration : IReportMigration
{
    private readonly XtraReportToDesignConverter inner = new();

    /// <inheritdoc cref="IReportMigration.Convert"/>
    public ReportMigrationResult Convert(string source)
        => Convert(source, resources: null);

    /// <summary>Converts the definition with caller-supplied report resources.</summary>
    /// <param name="source">The supported report definition or Designer source.</param>
    /// <param name="resources">Named textual resources, or null when no external resources are supplied.</param>
    /// <returns>The editable design and migration diagnostics.</returns>
    /// <exception cref="PvadLicenseException">Native Report Designer Migration rights for this provider are unavailable.</exception>
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
