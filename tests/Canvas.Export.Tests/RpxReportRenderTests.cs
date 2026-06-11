using System.Text;
using Canvas.Migration.Rpx;
using Canvas.WebApi.Infrastructure;

namespace Canvas.Export.Tests;

/// <summary>
/// End-to-end: a converted ActiveReports .rpx section report must render to a valid PDF through the same
/// pipeline the export endpoint uses (DesignJsonMapper → PdfDocument.ToBytes).
/// </summary>
public sealed class RpxReportRenderTests
{
    private const string Rpx = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report Name="Invoice">
          <Sections>
            <PageHeader Name="PageHeader1" Height="1">
              <Controls>
                <Label Name="title" Left="1" Top="0.1" Width="5" Height="0.4" Text="Invoice 2024" Font-FamilyName="Arial" Font-Size="20" Font-Bold="True" Alignment="Center" ForeColor="0, 102, 204" />
              </Controls>
            </PageHeader>
            <Detail Name="Detail1" Height="2">
              <Controls>
                <TextBox Name="customer" Left="1" Top="0" Width="3" Height="0.3" DataField="CustomerName" />
                <Line Name="rule" X1="1" Y1="0.5" X2="6" Y2="0.5" LineWeight="2" LineColor="Gray" />
                <Barcode Name="sku" Left="1" Top="1" Width="2" Height="0.5" Text="ABC-12345" Style="Code128" />
              </Controls>
            </Detail>
            <PageFooter Name="PageFooter1" Height="0.5">
              <Controls>
                <Label Name="pageinfo" Left="5" Top="0.1" Width="1" Height="0.2" Text="Page 1" />
              </Controls>
            </PageFooter>
          </Sections>
        </Report>
        """;

    [Fact]
    public void ConvertedRpxReport_RendersToValidPdf()
    {
        var design = new RpxToDesignConverter().Convert(Rpx).Design;

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 500, "PDF looks too small to contain the rendered report.");
    }
}
