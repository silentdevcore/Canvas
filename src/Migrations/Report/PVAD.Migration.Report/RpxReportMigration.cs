using PVAD.Licensing;
using PVAD.Migration.Report.Designer.Rpx;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

/// <summary>Imports ActiveReports RPX definitions into editable PVAD designs.</summary>
/// <remarks>Conversion requires Report Designer Migration rights for <c>rpx</c>.</remarks>
public sealed class RpxReportMigration : IReportMigration
{
    private readonly RpxToDesignConverter inner = new();

    /// <summary>Recognizes RPX-like text without starting a licensed conversion.</summary>
    /// <param name="source">Candidate report text.</param>
    /// <returns>True when RPX structure is detected.</returns>
    public static bool LooksLike(string source) => RpxToDesignConverter.LooksLikeRpx(source);

    /// <inheritdoc cref="IReportMigration.Convert"/>
    public ReportMigrationResult Convert(string source)
        => Convert(source, resources: null);

    /// <summary>Converts RPX with caller-supplied textual resources.</summary>
    /// <param name="source">The main RPX definition.</param>
    /// <param name="resources">Named resource texts, or null.</param>
    /// <returns>The editable design and conversion diagnostics.</returns>
    /// <exception cref="PvadLicenseException">Native Report Designer Migration rights for this provider are unavailable.</exception>
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
