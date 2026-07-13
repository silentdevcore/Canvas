using PxaDevExpressReportResourceParser = PXA.Migration.Report.Designer.DevExpress.DevExpressReportResourceParser;

namespace PXA.Migration.Report;

public static class DevExpressReportResourceParser
{
    public static Dictionary<string, string> ParseResx(string resxXml) =>
        PxaDevExpressReportResourceParser.ParseResx(resxXml);
}
