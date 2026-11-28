using PvadDevExpressReportResourceParser = PVAD.Migration.Report.Designer.DevExpress.DevExpressReportResourceParser;

namespace PVAD.Migration.Report;

/// <summary>Extracts named text values from DevExpress RESX resources.</summary>
/// <remarks>Parsing requires native Report Designer Migration rights for <c>devexpress-report</c>.
/// It does not deserialize CLR objects or execute report code.</remarks>
public static class DevExpressReportResourceParser
{
    /// <summary>Reads trimmed RESX data values without activating serialized resource types.</summary>
    /// <param name="resxXml">RESX XML; blank text produces an empty map after license admission.</param>
    /// <returns>An ordinal map of resource names to text values.</returns>
    /// <exception cref="ArgumentException">The supplied XML is malformed.</exception>
    /// <exception cref="PVAD.Licensing.PvadLicenseException">Native migration rights for DevExpress reports are unavailable.</exception>
    public static Dictionary<string, string> ParseResx(string resxXml) =>
        PvadDevExpressReportResourceParser.ParseResx(resxXml);

    internal static Dictionary<string, string> ParseResxForReport(string resxXml, string provider) =>
        PvadDevExpressReportResourceParser.ParseResxForReport(resxXml, provider);
}
