using PVAD.Licensing;
using PVAD.Migration.Report.Designer.Rdl;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

/// <summary>Imports RDL/RDLC report definitions into editable PVAD designs.</summary>
/// <remarks>Conversion requires Report Designer Migration rights for <c>rdl</c>.</remarks>
public sealed class RdlReportMigration : IReportMigration
{
    private readonly RdlToDesignConverter inner = new();

    /// <summary>Recognizes RDL-like text without performing licensed conversion.</summary>
    /// <param name="source">Candidate report text.</param>
    /// <returns>True when an RDL report definition is detected.</returns>
    public static bool LooksLike(string source) => RdlToDesignConverter.LooksLikeRdl(source);

    /// <inheritdoc cref="IReportMigration.Convert"/>
    /// <exception cref="PvadLicenseException">Native Report Designer Migration rights for this provider are unavailable.</exception>
    public ReportMigrationResult Convert(string source)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ReportDesignerMigration, "rdl");
        return ConvertAdmitted(source);
    }

    internal ReportMigrationResult ConvertAdmitted(string source)
    {
        PvadLicense.DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, "rdl");
        var result = inner.ConvertAutoAdmitted(source);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
