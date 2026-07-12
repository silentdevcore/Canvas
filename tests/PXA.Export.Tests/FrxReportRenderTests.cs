using System.Text;
using PXA.Migration.FastReport;
using PXA.WebApi.Infrastructure;

namespace PXA.Export.Tests;

/// <summary>
/// End-to-end: a converted FastReport .frx design must render to a valid PDF through the same pipeline
/// the export endpoint uses (DesignJsonMapper → PdfDocument.ToBytes).
/// </summary>
public sealed class FrxReportRenderTests
{
    private const string Frx = """
        <?xml version="1.0" encoding="utf-8"?>
        <Report ScriptLanguage="CSharp" ReportInfo.Name="Invoice">
          <ReportPage Name="Page1">
            <ReportTitleBand Name="ReportTitle1" Top="0" Width="718.2" Height="37.8">
              <TextObject Name="title" Left="0" Top="0" Width="718.2" Height="37.8" Text="INVOICE" HorzAlign="Center" Font="Tahoma, 14pt, style=Bold" TextFill.Color="Blue"/>
            </ReportTitleBand>
            <DataBand Name="Data1" Top="64" Width="718.2" Height="40" DataSource="Items">
              <TextObject Name="name" Left="0" Top="0" Width="200" Height="20" Text="[Items.Name]" Font="Tahoma, 9pt"/>
              <LineObject Name="rule" Left="0" Top="22" Width="718.2" Height="0" Border.Color="Gray" Border.Width="2"/>
              <BarcodeObject Name="bc" Left="500" Top="0" Width="150" Height="40" Text="ABC-12345" Barcode="Code128"/>
            </DataBand>
            <PageFooterBand Name="PageFooter1" Top="120" Width="718.2" Height="20">
              <TextObject Name="pageinfo" Left="600" Top="0" Width="100" Height="20" Text="Page 1"/>
            </PageFooterBand>
          </ReportPage>
        </Report>
        """;

    [Fact]
    public void ConvertedFrxReport_RendersToValidPdf()
    {
        var design = new FrxToDesignConverter().Convert(Frx).Design;

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 500, "PDF looks too small to contain the rendered report.");
    }
}
