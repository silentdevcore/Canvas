using System.Xml.Linq;

namespace PXA.Migration.DevExpressReport;

public static class DevExpressReportResourceParser
{
    public static Dictionary<string, string> ParseResx(string resxXml)
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
