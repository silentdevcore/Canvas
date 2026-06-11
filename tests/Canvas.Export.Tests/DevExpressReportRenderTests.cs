using System.Text;
using Canvas.Migration.DevExpressReport;
using Canvas.WebApi.Infrastructure;

namespace Canvas.Export.Tests;

/// <summary>
/// End-to-end: a converted DevExpress report design must render to a valid PDF through the same
/// pipeline the export endpoint uses (DesignJsonMapper → PdfDocument.ToBytes).
/// </summary>
public sealed class DevExpressReportRenderTests
{
    // Exercises a broad mix of mapped controls: title, line, table, checkbox, shape, and a page footer.
    private const string Repx = """
        <XtraReportsLayoutSerializer ControlType="DevExpress.XtraReports.UI.XtraReport, X" Name="Invoice" PaperKind="A4" Margins="100, 100, 100, 100">
          <Bands>
            <Item1 ControlType="DevExpress.XtraReports.UI.ReportHeaderBand, X" Name="ReportHeader" HeightF="120">
              <Controls>
                <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="title" Text="Invoice 2024" SizeF="400,40" LocationFloat="0,10" Font="Arial, 20pt, style=Bold" ForeColor="0, 102, 204" TextAlignment="MiddleCenter" />
                <Item2 ControlType="DevExpress.XtraReports.UI.XRLine, X" Name="rule" SizeF="400,2" LocationFloat="0,60" ForeColor="Gray" LineWidth="2" />
              </Controls>
            </Item1>
            <Item2 ControlType="DevExpress.XtraReports.UI.DetailBand, X" Name="Detail" HeightF="300">
              <Controls>
                <Item1 ControlType="DevExpress.XtraReports.UI.XRCheckBox, X" Name="paid" Text="Paid" SizeF="80,20" LocationFloat="0,0" CheckBoxState="Checked" />
                <Item2 ControlType="DevExpress.XtraReports.UI.XRTable, X" Name="items" SizeF="400,40" LocationFloat="0,40">
                  <Rows>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRTableRow, X" Name="r1">
                      <Cells>
                        <Item1 ControlType="DevExpress.XtraReports.UI.XRTableCell, X" Name="c1" Text="Item" TextAlignment="MiddleLeft" />
                        <Item2 ControlType="DevExpress.XtraReports.UI.XRTableCell, X" Name="c2" Text="Total" TextAlignment="MiddleRight" />
                      </Cells>
                    </Item1>
                  </Rows>
                </Item2>
              </Controls>
            </Item2>
            <Item3 ControlType="DevExpress.XtraReports.UI.PageFooterBand, X" Name="PageFooter" HeightF="40">
              <Controls>
                <Item1 ControlType="DevExpress.XtraReports.UI.XRPageInfo, X" Name="pageinfo" Text="Page 1" SizeF="100,20" LocationFloat="300,5" />
              </Controls>
            </Item3>
          </Bands>
        </XtraReportsLayoutSerializer>
        """;

    [Fact]
    public void ConvertedRepxReport_RendersToValidPdf()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(Repx).Design;

        var document = DesignJsonMapper.MapToPdfDocument(design);
        var bytes = document.ToBytes();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 500, "PDF looks too small to contain the rendered report.");
    }

    [Fact]
    public void ConvertedCSharpReport_RendersToValidPdf()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel a;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.a = new XRLabel();
                    this.a.Text = "Hello";
                    this.a.LocationF = new PointF(40F, 40F);
                    this.a.SizeF = new SizeF(200F, 30F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.a });
                }
            }
            """;
        var design = new XtraReportToDesignConverter().Convert(source).Design;

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
