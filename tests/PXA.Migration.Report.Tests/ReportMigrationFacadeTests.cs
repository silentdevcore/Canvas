using PXA.Migration.Abstractions;

namespace PXA.Migration.Report.Tests;

public sealed class ReportMigrationFacadeTests
{
    [Fact]
    public void ActiveReportsJs_ConvertsJsonToDesign()
    {
        var result = new ActiveReportsJsMigration().Convert("""
            {
              "reportType": "ActiveReportsJS",
              "name": "Invoice JS",
              "page": { "width": "8.5in", "height": "11in" },
              "body": {
                "reportItems": [
                  { "type": "textbox", "name": "title", "left": "1in", "top": "0.5in", "width": "4in", "height": "0.4in", "value": "Invoice" }
                ]
              }
            }
            """);

        Assert.Equal("Invoice JS", result.Design.Name);
        Assert.Contains(result.Design.Pages[0].Elements, element => element.Name == "title");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGARJS001");
    }

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
    public void FastReport_ConvertsFrxToDesign()
    {
        var result = new FastReportMigration().Convert("""
            <?xml version="1.0" encoding="utf-8"?>
            <Report ScriptLanguage="CSharp" ReportInfo.Name="Invoice">
              <ReportPage Name="Page1">
                <DataBand Name="Data1" Top="0" Width="718.2" Height="20">
                  <TextObject Name="name" Left="0" Top="0" Width="200" Height="20" Text="[Items.Name]"/>
                </DataBand>
              </ReportPage>
            </Report>
            """);

        Assert.Equal("Invoice", result.Design.Name);
        Assert.Contains(result.Design.Pages[0].Elements, element => element.Name == "name");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGFRX001");
    }

    [Fact]
    public void JasperReports_ConvertsJrxmlToDesign()
    {
        var result = new JasperReportsMigration().Convert("""
            <?xml version="1.0" encoding="UTF-8"?>
            <jasperReport xmlns="http://jasperreports.sourceforge.net/jasperreports" name="Invoice"
                pageWidth="595" pageHeight="842" columnWidth="555" leftMargin="20" rightMargin="20" topMargin="20" bottomMargin="20">
              <detail>
                <band height="40">
                  <textField>
                    <reportElement key="customer" x="0" y="0" width="200" height="20"/>
                    <textFieldExpression><![CDATA[$F{customerName}]]></textFieldExpression>
                  </textField>
                </band>
              </detail>
            </jasperReport>
            """);

        Assert.Equal("Invoice", result.Design.Name);
        Assert.Contains(result.Design.Pages[0].Elements, element => element.Name == "customer");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGJRXML001");
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

    [Fact]
    public void Stimulsoft_ConvertsMrtToDesign()
    {
        var result = new StimulsoftReportMigration().Convert("""
            <?xml version="1.0" encoding="utf-8"?>
            <StiSerializer version="1.02" type="Net" application="StiReport">
              <ReportName>Invoice</ReportName>
              <Pages isList="true" count="1">
                <Page1 Ref="1" type="Page" isKey="true">
                  <PaperSize>A4</PaperSize>
                  <Components isList="true" count="1">
                    <DataBand1 Ref="2" type="DataBand" isKey="true">
                      <ClientRectangle>0,80,749,40</ClientRectangle>
                      <Components isList="true" count="1">
                        <Text1 Ref="3" type="Text" isKey="true">
                          <ClientRectangle>0,0,300,20</ClientRectangle>
                          <Text>{Customers.CompanyName}</Text>
                          <Name>Text1</Name>
                        </Text1>
                      </Components>
                      <Name>DataBand1</Name>
                    </DataBand1>
                  </Components>
                  <Name>Page1</Name>
                </Page1>
              </Pages>
            </StiSerializer>
            """);

        Assert.Equal("Invoice", result.Design.Name);
        Assert.Contains(result.Design.Pages[0].Elements, element => element.Name == "Text1");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGMRT001");
    }

    [Fact]
    public void Telerik_ConvertsTrdxToDesign()
    {
        var result = new TelerikReportMigration().Convert("""
            <?xml version="1.0" encoding="utf-8"?>
            <Report Width="8.1in" Name="Invoice" xmlns="http://schemas.telerik.com/reporting/2012/3.6">
              <PageSettings><PaperKind>Letter</PaperKind><Margins Left="1in" Right="1in" Top="1in" Bottom="1in"/></PageSettings>
              <Items>
                <DetailSection Height="1in" Name="detailSection1">
                  <Items>
                    <TextBox Width="3in" Height="0.3in" Left="0in" Top="0in" Value="=Fields.CustomerName" Name="customer"/>
                  </Items>
                </DetailSection>
              </Items>
            </Report>
            """);

        Assert.Equal("Invoice", result.Design.Name);
        Assert.Contains(result.Design.Pages[0].Elements, element => element.Name == "customer");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGTRDX001");
    }
}
