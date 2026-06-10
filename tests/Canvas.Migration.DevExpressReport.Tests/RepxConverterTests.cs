using Canvas.Core.Contracts;
using Canvas.Migration.DevExpressReport;

namespace Canvas.Migration.DevExpressReport.Tests;

public sealed class RepxConverterTests
{
    // A representative .repx layout: ReportHeader (bound title) + Detail (line + table) + PageFooter.
    private const string SampleRepx = """
        <?xml version="1.0" encoding="utf-8"?>
        <XtraReportsLayoutSerializer SerializerVersion="23.1.5.0" Ref="1" ControlType="DevExpress.XtraReports.UI.XtraReport, DevExpress.XtraReports.v23.1" Name="InvoiceReport" Margins="100, 100, 100, 100" PaperKind="Letter">
          <Bands>
            <Item1 Ref="2" ControlType="DevExpress.XtraReports.UI.ReportHeaderBand, DevExpress.XtraReports.v23.1" Name="ReportHeader" HeightF="100">
              <Controls>
                <Item1 Ref="3" ControlType="DevExpress.XtraReports.UI.XRLabel, DevExpress.XtraReports.v23.1" Name="xrTitle" Text="Invoice 2024" SizeF="400,40" LocationFloat="50,20" Font="Tahoma, 18pt, style=Bold" ForeColor="Red" TextAlignment="MiddleCenter">
                  <ExpressionBindings>
                    <Item1 Ref="4" EventName="BeforePrint" PropertyName="Text" Expression="[CustomerName]" />
                  </ExpressionBindings>
                </Item1>
              </Controls>
            </Item1>
            <Item2 Ref="5" ControlType="DevExpress.XtraReports.UI.DetailBand, DevExpress.XtraReports.v23.1" Name="Detail" HeightF="200">
              <Controls>
                <Item1 Ref="6" ControlType="DevExpress.XtraReports.UI.XRLine, DevExpress.XtraReports.v23.1" Name="xrLine" SizeF="400,2" LocationFloat="50,10" ForeColor="Gray" />
                <Item2 Ref="7" ControlType="DevExpress.XtraReports.UI.XRTable, DevExpress.XtraReports.v23.1" Name="xrTable" SizeF="400,40" LocationFloat="50,30">
                  <Rows>
                    <Item1 Ref="8" ControlType="DevExpress.XtraReports.UI.XRTableRow, DevExpress.XtraReports.v23.1" Name="row1">
                      <Cells>
                        <Item1 Ref="9" ControlType="DevExpress.XtraReports.UI.XRTableCell, DevExpress.XtraReports.v23.1" Name="c1" Text="Name" />
                        <Item2 Ref="10" ControlType="DevExpress.XtraReports.UI.XRTableCell, DevExpress.XtraReports.v23.1" Name="c2" Text="Price" />
                      </Cells>
                    </Item1>
                  </Rows>
                </Item2>
              </Controls>
            </Item2>
            <Item3 Ref="11" ControlType="DevExpress.XtraReports.UI.PageFooterBand, DevExpress.XtraReports.v23.1" Name="PageFooter" HeightF="40">
              <Controls>
                <Item1 Ref="12" ControlType="DevExpress.XtraReports.UI.XRPageInfo, DevExpress.XtraReports.v23.1" Name="xrPage" Text="Page" SizeF="100,20" LocationFloat="450,5" />
              </Controls>
            </Item3>
          </Bands>
        </XtraReportsLayoutSerializer>
        """;

    private static ElementDto Page(DesignExportDto d, string name) => d.Pages[0].Elements.Single(e => e.Name == name);

    [Fact]
    public void ConvertRepx_ParsesReportPageBandsAndControls()
    {
        var result = new XtraReportToDesignConverter().ConvertRepx(SampleRepx);
        var design = result.Design;

        Assert.Equal("InvoiceReport", design.Name);
        Assert.Equal(612d, design.PageSettings!.Width, 1);   // Letter
        Assert.Equal(792d, design.PageSettings.Height, 1);
        Assert.Equal(3, design.Pages[0].Elements.Count);     // title, line, table
        Assert.Single(design.SharedElements);                // page footer
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP001");
    }

    [Fact]
    public void ConvertRepx_FlattensBandsAndConvertsUnits()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SampleRepx).Design;

        // Title in ReportHeader (band top = 100 margin). x=(100+50)*0.72, y=(100+20)*0.72.
        var title = Page(design, "xrTitle");
        Assert.Equal(108d, title.X, 1);
        Assert.Equal(86.4d, title.Y, 1);

        // Line in Detail (band top = 100 + 100). y=(200+10)*0.72.
        Assert.Equal(151.2d, Page(design, "xrLine").Y, 1);
        Assert.Equal("line", Page(design, "xrLine").Type);
    }

    [Fact]
    public void ConvertRepx_MapsStyleColorFontAndAlignment()
    {
        var title = Page(new XtraReportToDesignConverter().ConvertRepx(SampleRepx).Design, "xrTitle");

        Assert.Equal("text", title.Type);
        Assert.Equal("#FF0000", title.Style!["color"]);
        Assert.Equal("Tahoma", title.Style["fontFamily"]);
        Assert.Equal(18d, System.Convert.ToDouble(title.Style["fontSize"]));
        Assert.Equal("bold", title.Style["fontWeight"]);
        Assert.Equal("center", title.Style["textAlign"]);
    }

    [Fact]
    public void ConvertRepx_MapsExpressionBindingToCanvasBinding()
    {
        var title = Page(new XtraReportToDesignConverter().ConvertRepx(SampleRepx).Design, "xrTitle");

        Assert.Equal("CustomerName", title.Binding);
        Assert.Equal("{{CustomerName}}", title.Content);
    }

    [Fact]
    public void ConvertRepx_MapsTableRowsAndCells()
    {
        var table = Page(new XtraReportToDesignConverter().ConvertRepx(SampleRepx).Design, "xrTable");

        Assert.Equal("table", table.Type);
        Assert.Equal(new[] { "Name", "Price" }, table.CellData!.Single());
    }

    [Fact]
    public void ConvertRepx_PageFooterBecomesSharedElementAnchoredToBottom()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SampleRepx).Design;
        var footer = design.SharedElements.Single();

        Assert.Equal("xrPage", footer.Name);
        Assert.True(footer.Y > 650, $"footer should sit near the page bottom, was {footer.Y}");
    }

    [Fact]
    public void ConvertRepx_RgbColorString_ParsesToHex()
    {
        var repx = """
            <XtraReportsLayoutSerializer ControlType="DevExpress.XtraReports.UI.XtraReport, X" Name="R" PaperKind="A4">
              <Bands>
                <Item1 ControlType="DevExpress.XtraReports.UI.DetailBand, X" Name="Detail" HeightF="100">
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="a" Text="X" SizeF="100,20" LocationFloat="0,0" ForeColor="0, 102, 204" />
                  </Controls>
                </Item1>
              </Bands>
            </XtraReportsLayoutSerializer>
            """;

        var design = new XtraReportToDesignConverter().ConvertRepx(repx).Design;
        Assert.Equal("#0066CC", Page(design, "a").Style!["color"]);
    }

    [Fact]
    public void ConvertAuto_DetectsRepxVsCSharp()
    {
        var sut = new XtraReportToDesignConverter();

        var fromXml = sut.ConvertAuto(SampleRepx).Design;
        Assert.Equal("InvoiceReport", fromXml.Name);

        var csharp = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class CodeReport : XtraReport
            {
                private DetailBand Detail;
                private XRLabel xrA;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.xrA = new XRLabel();
                    this.xrA.Text = "Hi";
                    this.xrA.LocationF = new PointF(0F, 0F);
                    this.xrA.SizeF = new SizeF(50F, 20F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrA });
                }
            }
            """;
        Assert.Equal("CodeReport", sut.ConvertAuto(csharp).Design.Name);
    }

    [Fact]
    public void ConvertRepx_InvalidXml_Throws()
    {
        Assert.Throws<ArgumentException>(() => new XtraReportToDesignConverter().ConvertRepx("<not valid"));
    }
}
