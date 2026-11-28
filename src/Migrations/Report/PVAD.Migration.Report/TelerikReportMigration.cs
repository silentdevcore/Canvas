using PVAD.Licensing;
using PVAD.Migration.Report.Designer.Telerik;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

/// <summary>Imports Telerik TRDX report definitions into editable PVAD designs.</summary>
/// <remarks>Conversion requires Report Designer Migration rights for <c>telerik</c>.
/// Extract packaged input before passing its textual definition to this converter.</remarks>
public sealed class TelerikReportMigration : IReportMigration
{
    private readonly TrdxToDesignConverter inner = new();

    /// <summary>Recognizes TRDX-like text without performing licensed conversion.</summary>
    /// <param name="source">Candidate report text.</param>
    /// <returns>True when a Telerik report definition is detected.</returns>
    public static bool LooksLike(string source) => TrdxToDesignConverter.LooksLikeTrdx(source);

    /// <inheritdoc cref="IReportMigration.Convert"/>
    /// <exception cref="PvadLicenseException">Native Report Designer Migration rights for this provider are unavailable.</exception>
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
