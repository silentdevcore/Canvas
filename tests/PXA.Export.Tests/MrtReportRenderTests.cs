using System.Text;
using PXA.Migration.Report.Designer.Stimulsoft;
using PXA.WebApi.Infrastructure;

namespace PXA.Export.Tests;

/// <summary>
/// End-to-end: a converted Stimulsoft .mrt design must render to a valid PDF through the same pipeline
/// the export endpoint uses (DesignJsonMapper → PdfDocument.ToBytes).
/// </summary>
public sealed class MrtReportRenderTests
{
    private const string Mrt = """
        <?xml version="1.0" encoding="utf-8"?>
        <StiSerializer version="1.02" type="Net" application="StiReport">
          <ReportName>Invoice</ReportName>
          <Pages isList="true" count="1">
            <Page1 type="Page"><PaperSize>A4</PaperSize>
              <Components isList="true">
                <ReportTitleBand1 type="ReportTitleBand"><ClientRectangle>0,20,749,40</ClientRectangle>
                  <Components isList="true">
                    <Text1 type="Text"><ClientRectangle>0,0,749,40</ClientRectangle><Font>Arial,20,Bold,Point,False,0</Font>
                      <HorAlignment>Center</HorAlignment><Text>INVOICE</Text><TextBrush>[0:102:204]</TextBrush><Name>Text1</Name></Text1>
                  </Components><Name>ReportTitleBand1</Name>
                </ReportTitleBand1>
                <DataBand1 type="DataBand"><ClientRectangle>0,80,749,40</ClientRectangle>
                  <Components isList="true">
                    <Text2 type="Text"><ClientRectangle>0,0,300,20</ClientRectangle><Text>{Customers.CompanyName}</Text><Name>Text2</Name></Text2>
                    <Line1 type="HorizontalLinePrimitive"><ClientRectangle>0,30,749,1</ClientRectangle><Color>[128:128:128]</Color><Name>Line1</Name></Line1>
                  </Components><Name>DataBand1</Name>
                </DataBand1>
                <PageFooterBand1 type="PageFooterBand"><ClientRectangle>0,1071,749,20</ClientRectangle>
                  <Components isList="true">
                    <Text3 type="Text"><ClientRectangle>600,0,149,20</ClientRectangle><Text>{PageNofM}</Text><Name>Text3</Name></Text3>
                  </Components><Name>PageFooterBand1</Name>
                </PageFooterBand1>
              </Components><Name>Page1</Name>
            </Page1>
          </Pages>
        </StiSerializer>
        """;

    [Fact]
    public void ConvertedMrtReport_RendersToValidPdf()
    {
        var design = new MrtToDesignConverter().Convert(Mrt).Design;

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 500, "PDF looks too small to contain the rendered report.");
    }
}
