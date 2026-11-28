using PVAD.Licensing;
using PVAD.Migration.Report.Designer.JasperReports;
using PVAD.Core.Contracts;

namespace PVAD.Migration.Report;

/// <summary>Imports JasperReports JRXML into an editable PVAD design.</summary>
/// <remarks>Conversion requires Report Designer Migration rights for <c>jasperreports</c>.
/// Detection alone does not perform conversion, execute expressions or grant processing rights.</remarks>
public sealed class JasperReportsMigration : IReportMigration
{
    private readonly JrxmlToDesignConverter inner = new();

    /// <summary>Checks whether source resembles JRXML without starting a licensed conversion.</summary>
    /// <param name="source">Candidate report text.</param>
    /// <returns>True when the format recognizer identifies a JasperReports definition.</returns>
    public static bool LooksLike(string source) => JrxmlToDesignConverter.LooksLikeJrxml(source);

    /// <inheritdoc cref="IReportMigration.Convert"/>
    public ReportMigrationResult Convert(string source)
        => Convert(source, subreportSources: null);

    /// <summary>Converts JRXML and resolves available subreport definitions from the supplied map.</summary>
    /// <param name="source">The main JRXML report definition.</param>
    /// <param name="subreportSources">Named subreport source texts, or null.</param>
    /// <returns>The editable design with subreport and conversion diagnostics.</returns>
    /// <exception cref="PvadLicenseException">Native Report Designer Migration rights for this provider are unavailable.</exception>
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
