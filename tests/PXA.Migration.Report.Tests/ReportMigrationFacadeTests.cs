using PXA.Migration.Abstractions;

namespace PXA.Migration.Report.Tests;

public sealed class ReportMigrationFacadeTests
{
    [Fact]
    public void DevExpressReport_ConvertsRepxToDesign()
    {
        var result = new DevExpressReportMigration().Convert("""
            <?xml version="1.0" encoding="utf-8"?>
            <XtraReportsLayoutSerializer SerializerVersion="23.1.5.0" Ref="1" ControlType="DevExpress.XtraReports.UI.XtraReport, DevExpress.XtraReports.v23.1" Name="InvoiceReport" PaperKind="Letter">
              <Bands>
                <Item1 Ref="2" ControlType="DevExpress.XtraReports.UI.DetailBand, DevExpress.XtraReports.v23.1" Name="Detail" HeightF="100">
                  <Controls>
                    <Item1 Ref="3" ControlType="DevExpress.XtraReports.UI.XRLabel, DevExpress.XtraReports.v23.1" Name="title" Text="Invoice" SizeF="200,40" LocationFloat="50,20" />
                  </Controls>
                </Item1>
              </Bands>
            </XtraReportsLayoutSerializer>
            """);

        Assert.Equal("InvoiceReport", result.Design.Name);
        Assert.Contains(result.Design.Pages[0].Elements, element => element.Name == "title");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGDEVREP001");
    }

    [Fact]
    public void Rdl_ConvertsReportXmlToDesign()
    {
        var result = new RdlReportMigration().Convert("""
            <?xml version="1.0" encoding="utf-8"?>
            <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition" Name="Invoice">
              <Body>
                <ReportItems>
                  <Textbox Name="customer">
                    <Top>0in</Top><Left>1in</Left><Height>0.3in</Height><Width>3in</Width>
                    <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!CustomerName.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                  </Textbox>
                </ReportItems>
                <Height>2in</Height>
              </Body>
              <Page><PageHeight>11in</PageHeight><PageWidth>8.5in</PageWidth></Page>
            </Report>
            """);

        Assert.Equal("Invoice", result.Design.Name);
        Assert.Contains(result.Design.Pages[0].Elements, element => element.Name == "customer");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGRDL001");
    }

    [Fact]
    public void Rpx_ConvertsSectionReportXmlToDesign()
    {
        var result = new RpxReportMigration().Convert("""
            <?xml version="1.0" encoding="utf-8"?>
            <Report Name="Invoice">
              <Sections>
                <Detail Name="Detail1" Height="2">
                  <Controls>
                    <TextBox Name="customer" Left="1" Top="0" Width="3" Height="0.3" DataField="CustomerName" />
                  </Controls>
                </Detail>
              </Sections>
            </Report>
            """);

        Assert.Equal("Invoice", result.Design.Name);
        Assert.Contains(result.Design.Pages[0].Elements, element => element.Name == "customer");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "CANMIGRPX001" && diagnostic.Severity == MigrationDiagnosticSeverity.Info);
    }
}
