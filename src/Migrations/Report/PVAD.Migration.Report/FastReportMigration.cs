using PVAD.Licensing;
using PVAD.Migration.Report.Designer.FastReport;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

/// <summary>Imports FastReport FRX definitions into editable PVAD designs.</summary>
/// <remarks>Conversion requires Report Designer Migration rights for <c>fastreport</c>.</remarks>
public sealed class FastReportMigration : IReportMigration
{
    private readonly FrxToDesignConverter inner = new();

    /// <summary>Recognizes FRX-like text without performing licensed conversion.</summary>
    /// <param name="source">Candidate report text.</param>
    /// <returns>True when a FastReport definition is detected.</returns>
    public static bool LooksLike(string source) => FrxToDesignConverter.LooksLikeFrx(source);

    /// <inheritdoc cref="IReportMigration.Convert"/>
    /// <exception cref="PvadLicenseException">Native Report Designer Migration rights for this provider are unavailable.</exception>
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
