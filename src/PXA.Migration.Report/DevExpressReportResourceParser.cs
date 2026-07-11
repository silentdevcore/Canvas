using CanvasDevExpressReportResourceParser = Canvas.Migration.DevExpressReport.DevExpressReportResourceParser;

namespace PXA.Migration.Report;

public static class DevExpressReportResourceParser
{
    public static Dictionary<string, string> ParseResx(string resxXml) =>
        CanvasDevExpressReportResourceParser.ParseResx(resxXml);
}
