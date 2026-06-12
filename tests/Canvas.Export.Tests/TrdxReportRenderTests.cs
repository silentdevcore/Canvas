using System.Text;
using Canvas.Migration.Telerik;
using Canvas.WebApi.Infrastructure;

namespace Canvas.Export.Tests;

/// <summary>
/// End-to-end: a converted Telerik .trdx design must render to a valid PDF through the same pipeline
/// the export endpoint uses (DesignJsonMapper → PdfDocument.ToBytes).
/// </summary>
public sealed class TrdxReportRenderTests
{
    private const string Trdx = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report Width="8.1in" Name="Invoice" xmlns="http://schemas.telerik.com/reporting/2012/3.6">
          <PageSettings><PaperKind>Letter</PaperKind><Margins Left="1in" Right="1in" Top="1in" Bottom="1in"/></PageSettings>
          <Items>
            <PageHeaderSection Height="0.5in" Name="ph">
              <Items>
                <TextBox Width="3.5in" Height="0.3in" Left="0in" Top="0.1in" Value="INVOICE" Name="title">
                  <Style TextAlign="Center" Color="0, 102, 204"><Font Name="Arial" Size="20pt" Bold="True"/></Style>
                </TextBox>
              </Items>
            </PageHeaderSection>
            <DetailSection Height="1in" Name="d">
              <Items>
                <TextBox Width="3in" Height="0.3in" Left="0in" Top="0in" Value="=Fields.CustomerName" Name="customer"/>
                <Barcode Width="2in" Height="0.5in" Left="0in" Top="0.4in" Value="ABC-12345" Type="Code128" Name="bc"/>
              </Items>
            </DetailSection>
            <PageFooterSection Height="0.4in" Name="pf">
              <Items>
                <TextBox Width="1in" Height="0.2in" Left="6in" Top="0in" Value="Page 1" Name="pageinfo"/>
              </Items>
            </PageFooterSection>
          </Items>
        </Report>
        """;

    [Fact]
    public void ConvertedTrdxReport_RendersToValidPdf()
    {
        var design = new TrdxToDesignConverter().Convert(Trdx).Design;

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 500, "PDF looks too small to contain the rendered report.");
    }
}
