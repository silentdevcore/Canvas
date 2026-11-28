using PVAD.Licensing;
using PVAD.Migration.Report.Designer.Stimulsoft;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

/// <summary>Imports Stimulsoft MRT report definitions into editable PVAD designs.</summary>
/// <remarks>Conversion requires Report Designer Migration rights for <c>stimulsoft</c>.</remarks>
public sealed class StimulsoftReportMigration : IReportMigration
{
    private readonly MrtToDesignConverter inner = new();

    /// <summary>Recognizes MRT-like text without performing licensed conversion.</summary>
    /// <param name="source">Candidate report text.</param>
    /// <returns>True when a Stimulsoft report definition is detected.</returns>
    public static bool LooksLike(string source) => MrtToDesignConverter.LooksLikeMrt(source);

    /// <inheritdoc cref="IReportMigration.Convert"/>
    /// <exception cref="PvadLicenseException">Native Report Designer Migration rights for this provider are unavailable.</exception>
    public ReportMigrationResult Convert(string source)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ReportDesignerMigration, "stimulsoft");
        return ConvertAdmitted(source);
    }

    internal ReportMigrationResult ConvertAdmitted(string source)
    {
        PvadLicense.DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, "stimulsoft");
        var result = inner.ConvertAutoAdmitted(source);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
