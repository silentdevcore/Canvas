using System.Text.Json;
using System.Text.Json.Nodes;
using PVAD.Core.Contracts;

namespace PVAD.Testing;

public static class NativeReportMigrationFixtures
{
    public static IReadOnlyDictionary<string, string> Sources { get; } = new Dictionary<string, string>
    {
        ["activereports-js"] = """{"reportType":"ActiveReportsJS","name":"Synthetic report","page":{"width":595,"height":842},"items":[]}""",
        ["devexpress-report"] = """<XtraReportsLayoutSerializer Name="SyntheticReport" PaperKind="A4"><Bands><Item1 ControlType="DevExpress.XtraReports.UI.DetailBand" Name="Detail" HeightF="40"><Controls><Item1 ControlType="DevExpress.XtraReports.UI.XRLabel" Name="title" Text="Synthetic report" SizeF="150,20" LocationFloat="10,10" /></Controls></Item1></Bands></XtraReportsLayoutSerializer>""",
        ["fastreport"] = """<Report ReportName="SyntheticReport"><ReportPage Name="Page1" PaperWidth="210" PaperHeight="297"><DataBand Name="Data" Height="40"><TextObject Name="title" Text="Synthetic report" Width="150" Height="20" /></DataBand></ReportPage></Report>""",
        ["jasperreports"] = """<jasperReport name="SyntheticReport" pageWidth="595" pageHeight="842"><detail><band height="40"><staticText><reportElement x="10" y="10" width="150" height="20"/><text>Synthetic report</text></staticText></band></detail></jasperReport>""",
        ["rdl"] = """<Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition"><Description>Synthetic report</Description><Body><ReportItems><Textbox Name="title"><Top>0.1in</Top><Left>0.1in</Left><Width>2in</Width><Height>0.3in</Height><Value>Synthetic report</Value></Textbox></ReportItems><Height>1in</Height></Body><Width>8in</Width></Report>""",
        ["rpx"] = """<Report Name="SyntheticReport"><Sections><Detail Name="Detail" Height="1"><Controls><Control Type="AR.Label" Name="title" Left="0.1" Top="0.1" Width="2" Height="0.3" Text="Synthetic report"/></Controls></Detail></Sections></Report>""",
        ["stimulsoft"] = """<StiSerializer application="StiReport"><ReportName>SyntheticReport</ReportName><Pages><Page1 type="Page"><Name>Page1</Name><PaperSize>A4</PaperSize><Components><DataBand1 type="DataBand"><Name>Detail</Name><ClientRectangle>0,0,700,40</ClientRectangle><Components><Text1 type="Text"><Name>title</Name><ClientRectangle>10,10,150,20</ClientRectangle><Text>Synthetic report</Text></Text1></Components></DataBand1></Components></Page1></Pages></StiSerializer>""",
        ["telerik"] = """<Report xmlns="http://schemas.telerik.com/reporting/2024/1.0" Name="SyntheticReport" Width="8in"><Items><DetailSection Name="Detail" Height="1in"><Items><TextBox Name="title" Value="Synthetic report" Width="2in" Height="0.3in" /></Items></DetailSection></Items></Report>""",
    };

    public const string DevExpressCSharp = """
        public class SyntheticReport : DevExpress.XtraReports.UI.XtraReport
        {
            private DevExpress.XtraReports.UI.XRLabel title;
            void InitializeComponent() {
                this.title = new DevExpress.XtraReports.UI.XRLabel();
                this.title.Text = "Synthetic report";
            }
        }
        """;
    public const string ResourceXml = "<root><data name=\"synthetic\"><value>synthetic resource</value></data></root>";

    public static string Comparable(DesignExportDto design, object diagnostics)
    {
        if (design.Id.Length < 32 || !Guid.TryParseExact(design.Id[^32..], "N", out _))
            throw new InvalidOperationException("Expected the existing generated report identity.");
        var node = JsonSerializer.SerializeToNode(design)!.AsObject();
        node[nameof(design.Id)] = design.Id[..^32] + new string('0', 32);
        return node.ToJsonString() + "\n" + JsonSerializer.Serialize(diagnostics);
    }
}
