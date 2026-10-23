using PvadDevExpressReportResourceParser = PVAD.Migration.Report.Designer.DevExpress.DevExpressReportResourceParser;

namespace PVAD.Migration.Report;

public static class DevExpressReportResourceParser
{
    public static Dictionary<string, string> ParseResx(string resxXml) =>
        PvadDevExpressReportResourceParser.ParseResx(resxXml);
}
