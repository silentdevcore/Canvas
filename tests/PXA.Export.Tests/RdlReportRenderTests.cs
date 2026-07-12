using System.Text;
using PXA.Migration.Rdl;
using PXA.WebApi.Infrastructure;

namespace PXA.Export.Tests;

/// <summary>
/// End-to-end: a converted RDL report design must render to a valid PDF through the same pipeline the
/// export endpoint uses (DesignJsonMapper → PdfDocument.ToBytes).
/// </summary>
public sealed class RdlReportRenderTests
{
    // A page header title, a body line + 2-column tablix + embedded image, and a page footer.
    private const string Rdl = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition" Name="Invoice">
          <Body>
            <ReportItems>
              <Line Name="rule"><Top>0.2in</Top><Left>1in</Left><Height>0in</Height><Width>5in</Width>
                <Style><Border><Color>Gray</Color><Width>2pt</Width></Border></Style></Line>
              <Image Name="logo"><Top>0.5in</Top><Left>1in</Left><Height>0.5in</Height><Width>0.5in</Width>
                <Source>Embedded</Source><Value>brand</Value></Image>
              <CustomReportItem Name="sku"><Type>Barcode</Type><Top>0.5in</Top><Left>3in</Left><Height>0.5in</Height><Width>2in</Width>
                <CustomProperties>
                  <CustomProperty><Name>Symbology</Name><Value>Code128</Value></CustomProperty>
                  <CustomProperty><Name>Value</Name><Value>ABC-12345</Value></CustomProperty>
                </CustomProperties>
              </CustomReportItem>
              <Subreport Name="detail"><Top>2.4in</Top><Left>1in</Left><Height>0.5in</Height><Width>4in</Width><ReportName>Detail</ReportName></Subreport>
              <Tablix Name="items"><Top>1.2in</Top><Left>1in</Left><Height>1in</Height><Width>4in</Width>
                <TablixBody>
                  <TablixColumns><TablixColumn><Width>2in</Width></TablixColumn><TablixColumn><Width>2in</Width></TablixColumn></TablixColumns>
                  <TablixRows>
                    <TablixRow><TablixCells>
                      <TablixCell><CellContents><Textbox Name="h1"><Paragraphs><Paragraph><TextRuns><TextRun><Value>Item</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                      <TablixCell><CellContents><Textbox Name="h2"><Paragraphs><Paragraph><TextRuns><TextRun><Value>Total</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox></CellContents></TablixCell>
                    </TablixCells></TablixRow>
                  </TablixRows>
                </TablixBody>
              </Tablix>
            </ReportItems>
            <Height>5in</Height>
          </Body>
          <Page>
            <PageHeader><Height>1in</Height><ReportItems>
              <Textbox Name="title"><Top>0.1in</Top><Left>1in</Left><Height>0.4in</Height><Width>5in</Width>
                <Paragraphs><Paragraph><Style><TextAlign>Center</TextAlign></Style><TextRuns><TextRun><Value>Invoice 2024</Value><Style><FontSize>20pt</FontSize><FontWeight>Bold</FontWeight></Style></TextRun></TextRuns></Paragraph></Paragraphs>
              </Textbox>
            </ReportItems></PageHeader>
            <PageFooter><Height>0.5in</Height><ReportItems>
              <Textbox Name="pageinfo"><Top>0.1in</Top><Left>4in</Left><Height>0.3in</Height><Width>1in</Width>
                <Paragraphs><Paragraph><TextRuns><TextRun><Value>Page 1</Value></TextRun></TextRuns></Paragraph></Paragraphs></Textbox>
            </ReportItems></PageFooter>
            <PageHeight>11in</PageHeight><PageWidth>8.5in</PageWidth>
            <LeftMargin>1in</LeftMargin><RightMargin>1in</RightMargin><TopMargin>1in</TopMargin><BottomMargin>1in</BottomMargin>
          </Page>
          <EmbeddedImages><EmbeddedImage Name="brand"><MIMEType>image/png</MIMEType>
            <ImageData>iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=</ImageData>
          </EmbeddedImage></EmbeddedImages>
        </Report>
        """;

    [Fact]
    public void ConvertedRdlReport_RendersToValidPdf()
    {
        var design = new RdlToDesignConverter().Convert(Rdl).Design;

        var document = DesignJsonMapper.MapToPdfDocument(design);
        var bytes = document.ToBytes();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 500, "PDF looks too small to contain the rendered report.");
    }
}
