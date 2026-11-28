using PVAD.Licensing;
using PVAD.Migration.Report.Designer.ActiveReportsJs;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

/// <summary>Imports ActiveReportsJS report definitions into editable PVAD designs.</summary>
/// <remarks>Conversion requires Report Designer Migration rights for <c>activereports-js</c>.</remarks>
public sealed class ActiveReportsJsMigration : IReportMigration
{
    private readonly ActiveReportsJsToDesignConverter inner = new();

    /// <summary>Recognizes ActiveReportsJS report text without performing conversion.</summary>
    /// <param name="source">Candidate report text.</param>
    /// <returns>True when the format recognizer identifies an ActiveReportsJS definition.</returns>
    public static bool LooksLike(string source) => ActiveReportsJsToDesignConverter.LooksLikeActiveReportsJs(source);

    /// <inheritdoc cref="IReportMigration.Convert"/>
    /// <exception cref="PvadLicenseException">Native Report Designer Migration rights for this provider are unavailable.</exception>
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
