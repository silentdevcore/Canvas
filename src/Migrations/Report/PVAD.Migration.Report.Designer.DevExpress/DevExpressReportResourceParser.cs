using System.Xml.Linq;
using PVAD.Licensing;

namespace PVAD.Migration.Report.Designer.DevExpress;

public static class DevExpressReportResourceParser
{
    public static Dictionary<string, string> ParseResx(string resxXml)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ReportDesignerMigration, "devexpress-report");
        return ParseCore(resxXml);
    }

    internal static Dictionary<string, string> ParseResxForReport(string resxXml, string provider)
    {
        PvadLicense.DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, provider);
        return ParseCore(resxXml);
    }

    private static Dictionary<string, string> ParseCore(string resxXml)
    {
        if (string.IsNullOrWhiteSpace(resxXml))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        XDocument doc;
        try
        {
            doc = XDocument.Parse(resxXml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new ArgumentException($"Invalid .resx XML: {ex.Message}", nameof(resxXml), ex);
        }
        var resources = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var data in doc.Descendants().Where(e => e.Name.LocalName == "data"))
        {
            var name = data.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var value = data.Elements().FirstOrDefault(e => e.Name.LocalName == "value")?.Value;
            if (value is null) continue;

            resources[name] = value.Trim();
        }

        return resources;
    }
}
