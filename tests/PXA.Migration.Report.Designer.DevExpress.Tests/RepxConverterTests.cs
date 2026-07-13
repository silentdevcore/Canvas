using PXA.Core.Contracts;
using PXA.Migration.Report.Designer.DevExpress;

namespace PXA.Migration.Report.Designer.DevExpress.Tests;

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
    public void ConvertRepx_MapsExpressionBindingToPxaBinding()
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
    public void ConvertRepx_DetailReportBand_StacksNestedBands()
    {
        var repx = """
            <XtraReportsLayoutSerializer ControlType="DevExpress.XtraReports.UI.XtraReport, X" Name="R" PaperKind="A4" Margins="0,0,0,0">
              <Bands>
                <Item1 ControlType="DevExpress.XtraReports.UI.DetailBand, X" Name="Detail" HeightF="100">
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="body" Text="Body" SizeF="100,20" LocationFloat="0,0" />
                  </Controls>
                </Item1>
                <Item2 ControlType="DevExpress.XtraReports.UI.DetailReportBand, X" Name="LinesReport" HeightF="20">
                  <Bands>
                    <Item1 ControlType="DevExpress.XtraReports.UI.DetailBand, X" Name="LinesDetail" HeightF="30">
                      <Controls>
                        <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="line" Text="Line" SizeF="100,20" LocationFloat="0,5" />
                      </Controls>
                    </Item1>
                  </Bands>
                </Item2>
              </Bands>
            </XtraReportsLayoutSerializer>
            """;

        var result = new XtraReportToDesignConverter().ConvertRepx(repx);

        Assert.Equal(0d, Page(result.Design, "body").Y, 1);
        Assert.Equal(90d, Page(result.Design, "line").Y, 1);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP014");
    }

    [Fact]
    public void ConvertRepx_GroupBands_EmitGroupSemanticsDiagnostic()
    {
        var repx = """
            <XtraReportsLayoutSerializer ControlType="DevExpress.XtraReports.UI.XtraReport, X" Name="R" PaperKind="A4" Margins="0,0,0,0">
              <Bands>
                <Item1 ControlType="DevExpress.XtraReports.UI.GroupHeaderBand, X" Name="CustomerHeader" HeightF="40">
                  <GroupFields>
                    <Item1 FieldName="CustomerId" SortOrder="Ascending" />
                  </GroupFields>
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="header" Text="Customer" SizeF="100,20" LocationFloat="0,0" />
                  </Controls>
                </Item1>
                <Item2 ControlType="DevExpress.XtraReports.UI.DetailBand, X" Name="Detail" HeightF="100">
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="detail" Text="Line" SizeF="100,20" LocationFloat="0,0" />
                  </Controls>
                </Item2>
                <Item3 ControlType="DevExpress.XtraReports.UI.GroupFooterBand, X" Name="CustomerFooter" HeightF="30">
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="footer" Text="Subtotal" SizeF="100,20" LocationFloat="0,0" />
                  </Controls>
                </Item3>
              </Bands>
            </XtraReportsLayoutSerializer>
            """;

        var result = new XtraReportToDesignConverter().ConvertRepx(repx);

        Assert.Equal(0d, Page(result.Design, "header").Y, 1);
        Assert.Equal(28.8d, Page(result.Design, "detail").Y, 1);
        Assert.Equal(100.8d, Page(result.Design, "footer").Y, 1);
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "CANMIGDEVREP015" && d.Message.Contains("CustomerHeader", StringComparison.Ordinal));
        Assert.Contains("CustomerId", diagnostic.Message);
    }

    [Fact]
    public void ConvertRepx_TextLayoutHints_MapToStyleAndDiagnostics()
    {
        var result = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """
            <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X"
                   Name="notes"
                   Text="Line 1&#xA;Line 2"
                   SizeF="200,40"
                   LocationFloat="0,0"
                   Multiline="true"
                   WordWrap="true"
                   CanGrow="true"
                   CanShrink="true"
                   KeepTogether="true"
                   AnchorHorizontal="Both"
                   AnchorVertical="Bottom" />
            """));

        var notes = Page(result.Design, "notes");

        Assert.Equal("pre-wrap", notes.Style!["whiteSpace"]);
        Assert.Equal("visible", notes.Style["overflow"]);
        Assert.Equal(true, notes.Style["devExpressCanShrink"]);
        Assert.Equal(true, notes.Style["devExpressKeepTogether"]);
        Assert.Equal("Both", notes.Style["devExpressAnchorHorizontal"]);
        Assert.Equal("Bottom", notes.Style["devExpressAnchorVertical"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP016");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP017");
    }

    [Fact]
    public void ConvertRepx_TextFitModeAndTextTrimming_MapToStyleMetadataAndDiagnostic()
    {
        var result = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """<Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="notes" Text="Long text" SizeF="100,20" LocationFloat="0,0" TextFitMode="ShrinkOnly" TextTrimming="Word" />"""));

        var style = Page(result.Design, "notes").Style!;
        Assert.Equal("ShrinkOnly", style["devExpressTextFitMode"]);
        Assert.Equal("Word", style["devExpressTextTrimming"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP023");
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

    private static string SingleControlRepx(string controlXml) => $"""
        <XtraReportsLayoutSerializer ControlType="DevExpress.XtraReports.UI.XtraReport, X" Name="R" PaperKind="A4">
          <Bands>
            <Item1 ControlType="DevExpress.XtraReports.UI.DetailBand, X" Name="Detail" HeightF="100">
              <Controls>{controlXml}</Controls>
            </Item1>
          </Bands>
        </XtraReportsLayoutSerializer>
        """;

    [Fact]
    public void ConvertRepx_XRCheckBox_BecomesCheckmark()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """<Item1 ControlType="DevExpress.XtraReports.UI.XRCheckBox, X" Name="chk" Text="Agree" SizeF="100,20" LocationFloat="0,0" CheckBoxState="Checked" />"""))
            .Design;

        var el = Page(design, "chk");
        Assert.Equal("checkmark", el.Type);
        Assert.Equal("checked", el.CheckState);
    }

    [Fact]
    public void ConvertRepx_XRShapeEllipse_BecomesCircle()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """
            <Item1 ControlType="DevExpress.XtraReports.UI.XRShape, X" Name="shp" SizeF="60,60" LocationFloat="0,0">
              <Shape ControlType="DevExpress.XtraPrinting.Shape.ShapeEllipse, X" />
            </Item1>
            """)).Design;

        Assert.Equal("circle", Page(design, "shp").Type);
    }

    [Fact]
    public void ConvertRepx_XRShapeArrow_BecomesPxaArrowWithDiagnostic()
    {
        var result = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """
            <Item1 ControlType="DevExpress.XtraReports.UI.XRShape, X" Name="arrow" SizeF="120,20" LocationFloat="0,0" BorderColor="Red" BorderWidth="2">
              <Shape ControlType="DevExpress.XtraPrinting.Shape.ShapeArrow, X" />
            </Item1>
            """));

        var arrow = Page(result.Design, "arrow");
        Assert.Equal("arrow", arrow.Type);
        Assert.Equal("arrow", arrow.EndMarker);
        Assert.Equal("#FF0000", arrow.Style!["color"]);
        Assert.Equal(2d, System.Convert.ToDouble(arrow.Style["strokeWidth"]));
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP019");
    }

    [Fact]
    public void ConvertRepx_Borders_MapPerSideBorderStyle()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """<Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="box" Text="Bordered" SizeF="100,20" LocationFloat="0,0" Borders="Left, Top" BorderColor="Blue" BorderWidth="2" />"""))
            .Design;

        var style = Page(design, "box").Style!;
        Assert.Equal(2d, System.Convert.ToDouble(style["borderLeftWidth"]));
        Assert.Equal(2d, System.Convert.ToDouble(style["borderTopWidth"]));
        Assert.Equal("#0000FF", style["borderLeftColor"]);
        Assert.Equal("#0000FF", style["borderTopColor"]);
        Assert.False(style.ContainsKey("borderRightWidth"));
    }

    [Fact]
    public void ConvertRepx_LineWidthAndDashStyle_MapToStrokeStyle()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """<Item1 ControlType="DevExpress.XtraReports.UI.XRLine, X" Name="ln" SizeF="200,2" LocationFloat="0,0" LineWidth="3" LineStyle="Dash" />"""))
            .Design;

        var style = Page(design, "ln").Style!;
        Assert.Equal(3d, System.Convert.ToDouble(style["strokeWidth"]));
        Assert.Equal("dashed", style["dashStyle"]);
    }

    [Fact]
    public void ConvertRepx_NestedPanelControls_AreFlattenedToAbsolutePositions()
    {
        // A panel at (50,30) in the Detail band contains a label at (10,5) relative to the panel.
        var design = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """
            <Item1 ControlType="DevExpress.XtraReports.UI.XRPanel, X" Name="panel" SizeF="200,80" LocationFloat="50,30">
              <Controls>
                <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="inner" Text="Inside" SizeF="100,20" LocationFloat="10,5" />
              </Controls>
            </Item1>
            """)).Design;

        // Margins default to 100. Panel: x=(100+50)*0.72=108, y=(100+30)*0.72=93.6.
        var panel = Page(design, "panel");
        Assert.Equal("rect", panel.Type);
        Assert.Equal(108d, panel.X, 1);

        // Inner label absolute: x=(100 + 50 + 10)*0.72=115.2, y=(100 + 30 + 5)*0.72=97.2.
        var inner = Page(design, "inner");
        Assert.Equal(115.2d, inner.X, 1);
        Assert.Equal(97.2d, inner.Y, 1);
    }

    [Fact]
    public void ConvertRepx_LabelBackgroundAndUnderline_MapToStyle()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """<Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="lbl" Text="Hi" SizeF="100,20" LocationFloat="0,0" BackColor="255, 255, 0" Font="Tahoma, 12pt, style=Underline" />"""))
            .Design;

        var style = Page(design, "lbl").Style!;
        Assert.Equal("#FFFF00", style["backgroundColor"]);
        Assert.Equal("underline", style["textDecoration"]);
    }

    [Fact]
    public void ConvertRepx_LabelWithoutBackColor_HasNoBackground()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """<Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="lbl" Text="Hi" SizeF="100,20" LocationFloat="0,0" />"""))
            .Design;

        Assert.False(Page(design, "lbl").Style!.ContainsKey("backgroundColor"));
    }

    [Fact]
    public void ConvertRepx_XRSubreport_BecomesPositionedPlaceholderWithDiagnostic()
    {
        var result = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """<Item1 ControlType="DevExpress.XtraReports.UI.XRSubreport, X" Name="sub" SizeF="100,20" LocationFloat="0,0" />"""));

        var sub = Page(result.Design, "sub");
        Assert.Equal("subsection", sub.Type);
        Assert.Contains("Subreport", sub.Content);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP012");
    }

    [Fact]
    public void ConvertRepx_VisibleExpressionBinding_MapsToVisibleExpression()
    {
        var result = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """
            <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="comment" Text="Comment" SizeF="100,20" LocationFloat="0,0">
              <ExpressionBindings>
                <Item1 PropertyName="Visible" Expression="Len([Comment]) &gt; 0" />
              </ExpressionBindings>
            </Item1>
            """));

        var comment = Page(result.Design, "comment");
        Assert.Equal("Len([Comment]) > 0", comment.VisibleExpression);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP020");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CANMIGDEVREP010" && d.Message.Contains("Visible", StringComparison.Ordinal));
    }

    [Fact]
    public void ConvertRepx_XRChart_BecomesPxaChartPlaceholder()
    {
        var result = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """<Item1 ControlType="DevExpress.XtraReports.UI.XRChart, X" Name="sales" SizeF="300,160" LocationFloat="10,20" />"""));

        var chart = Page(result.Design, "sales");

        Assert.Equal("chart", chart.Type);
        Assert.Equal("bar", chart.ChartType);
        Assert.NotNull(chart.ChartData);
        Assert.Equal(86.4d, chart.Y, 1); // default top margin 100 + detail band 0 + local 20, then ×0.72
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP018");
    }

    [Fact]
    public void ConvertRepx_XRGaugeAndXRPivotGrid_BecomePositionedPlaceholders()
    {
        var repx = """
            <XtraReportsLayoutSerializer ControlType="DevExpress.XtraReports.UI.XtraReport, X" Name="R" PaperKind="A4" Margins="0,0,0,0">
              <Bands>
                <Item1 ControlType="DevExpress.XtraReports.UI.DetailBand, X" Name="Detail" HeightF="240">
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRGauge, X" Name="gauge" SizeF="120,80" LocationFloat="0,0" />
                    <Item2 ControlType="DevExpress.XtraReports.UI.XRPivotGrid, X" Name="pivot" SizeF="240,120" LocationFloat="0,100" />
                  </Controls>
                </Item1>
              </Bands>
            </XtraReportsLayoutSerializer>
            """;

        var result = new XtraReportToDesignConverter().ConvertRepx(repx);

        Assert.Contains("Gauge", Page(result.Design, "gauge").Content);
        Assert.Contains("PivotGrid", Page(result.Design, "pivot").Content);
        Assert.Equal(72d, Page(result.Design, "pivot").Y, 1);
        Assert.Equal(2, result.Diagnostics.Count(d => d.Id == "CANMIGDEVREP018"));
    }

    [Fact]
    public void ConvertRepx_WithScripts_EmitsScriptDiagnostic()
    {
        var repx = """
            <XtraReportsLayoutSerializer ControlType="DevExpress.XtraReports.UI.XtraReport, X" Name="R" PaperKind="A4" ScriptLanguage="CSharp">
              <Bands>
                <Item1 ControlType="DevExpress.XtraReports.UI.DetailBand, X" Name="Detail" HeightF="100">
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="a" Text="X" SizeF="50,20" LocationFloat="0,0" />
                  </Controls>
                </Item1>
              </Bands>
              <Scripts><Item1 Name="OnBeforePrint" Script="// code" /></Scripts>
            </XtraReportsLayoutSerializer>
            """;
        var result = new XtraReportToDesignConverter().ConvertRepx(repx);

        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP012");
    }

    [Fact]
    public void ConvertRepx_TableHeaderAlignments_BecomeColumnAlignments()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """
            <Item1 ControlType="DevExpress.XtraReports.UI.XRTable, X" Name="t" SizeF="300,40" LocationFloat="0,0">
              <Rows>
                <Item1 ControlType="DevExpress.XtraReports.UI.XRTableRow, X" Name="r1">
                  <Cells>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRTableCell, X" Name="c1" Text="Name" TextAlignment="MiddleLeft" />
                    <Item2 ControlType="DevExpress.XtraReports.UI.XRTableCell, X" Name="c2" Text="Total" TextAlignment="MiddleRight" />
                  </Cells>
                </Item1>
              </Rows>
            </Item1>
            """)).Design;

        var table = Page(design, "t");
        Assert.Equal(new[] { "left", "right" }, table.ColumnAlignments);
    }

    [Fact]
    public void ConvertRepx_GroupHeaderBand_AddsRepeatAndGroupMetadata()
    {
        var repx = """
            <?xml version="1.0" encoding="utf-8"?>
            <XtraReportsLayoutSerializer ControlType="DevExpress.XtraReports.UI.XtraReport, X" Name="R" PaperKind="Letter">
              <Bands>
                <Item1 ControlType="DevExpress.XtraReports.UI.GroupHeaderBand, X" Name="GroupHeader1" HeightF="30">
                  <GroupFields>
                    <Item1 FieldName="Region" SortOrder="Ascending" />
                  </GroupFields>
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="regionLabel" Text="Region" SizeF="200,20" LocationFloat="0,0" />
                  </Controls>
                </Item1>
                <Item2 ControlType="DevExpress.XtraReports.UI.DetailBand, X" Name="Detail" HeightF="20">
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="cell" Text="x" SizeF="200,20" LocationFloat="0,0" />
                  </Controls>
                </Item2>
                <Item3 ControlType="DevExpress.XtraReports.UI.GroupFooterBand, X" Name="GroupFooter1" HeightF="20">
                  <GroupFields>
                    <Item1 FieldName="Region" />
                  </GroupFields>
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="regionTotal" Text="Total" SizeF="200,20" LocationFloat="0,0" />
                  </Controls>
                </Item3>
              </Bands>
            </XtraReportsLayoutSerializer>
            """;
        var result = new XtraReportToDesignConverter().ConvertRepx(repx);

        var header = Page(result.Design, "regionLabel");
        Assert.NotNull(header.Repeat);
        Assert.Equal("Region", header.Repeat!.DataPath);
        Assert.Equal(header.Id, header.Repeat.TemplateId);
        var headerGroup = Assert.IsType<Dictionary<string, object>>(header.Style!["devExpressGroup"]);
        Assert.Equal("header", headerGroup["role"]);
        Assert.Equal(new[] { "Region (Ascending)" }, Assert.IsType<string[]>(headerGroup["fields"]));

        var footer = Page(result.Design, "regionTotal");
        var footerGroup = Assert.IsType<Dictionary<string, object>>(footer.Style!["devExpressGroup"]);
        Assert.Equal("footer", footerGroup["role"]);
        Assert.Equal("Region", footer.Repeat!.DataPath);

        // A plain Detail-band control gets no group repeat metadata.
        var detail = Page(result.Design, "cell");
        Assert.Null(detail.Repeat);

        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP015");
    }

    [Fact]
    public void ConvertRepx_GroupFooterAggregate_BecomesAggregateExpression()
    {
        // An XRLabel in a group footer bound to Sum([Total]) aggregates over that group's dataset scope.
        var repx = """
            <?xml version="1.0" encoding="utf-8"?>
            <XtraReportsLayoutSerializer ControlType="DevExpress.XtraReports.UI.XtraReport, X" Name="R" PaperKind="Letter">
              <Bands>
                <Item1 ControlType="DevExpress.XtraReports.UI.GroupFooterBand, X" Name="GroupFooter1" HeightF="20">
                  <GroupFields>
                    <Item1 FieldName="Region" />
                  </GroupFields>
                  <Controls>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRLabel, X" Name="regionTotal" Text="Total" SizeF="200,20" LocationFloat="0,0">
                      <ExpressionBindings>
                        <Item1 PropertyName="Text" Expression="Sum([Total])" />
                      </ExpressionBindings>
                    </Item1>
                  </Controls>
                </Item1>
              </Bands>
            </XtraReportsLayoutSerializer>
            """;
        var result = new XtraReportToDesignConverter().ConvertRepx(repx);

        var total = Page(result.Design, "regionTotal");
        Assert.Equal("$sum($group, \"Total\")", total.Expression);                  // group-scoped aggregate
        Assert.Equal("Sum([Total])", total.Style!["devExpressExpression"]);         // raw preserved
    }

    [Fact]
    public void ConvertRepx_TableCellStyles_FillBorderFontAlign()
    {
        var design = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            """
            <Item1 ControlType="DevExpress.XtraReports.UI.XRTable, X" Name="t" SizeF="300,40" LocationFloat="0,0">
              <Rows>
                <Item1 ControlType="DevExpress.XtraReports.UI.XRTableRow, X" Name="r1">
                  <Cells>
                    <Item1 ControlType="DevExpress.XtraReports.UI.XRTableCell, X" Name="c1" Text="Total"
                           BackColor="Yellow" ForeColor="Red" TextAlignment="MiddleCenter"
                           Font="Verdana, 12pt, style=Bold" Borders="Bottom" BorderColor="Black" BorderWidth="2" />
                  </Cells>
                </Item1>
              </Rows>
            </Item1>
            """)).Design;

        var table = Page(design, "t");
        var cs = Assert.Single(table.CellStyles!);
        Assert.Equal("#FFFF00", cs.BackgroundColor);
        Assert.Equal("#FF0000", cs.Color);
        Assert.Equal("center", cs.TextAlign);
        Assert.Equal("Verdana", cs.FontFamily);
        Assert.True(cs.Bold);
        Assert.NotNull(cs.BorderBottom);
        Assert.Equal(2, cs.BorderBottom!.Width);
        Assert.Null(cs.BorderTop);
    }

    [Fact]
    public void ConvertRepx_XRPictureBox_WithEmbeddedImage_KeepsDataUrl()
    {
        const string pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var result = new XtraReportToDesignConverter().ConvertRepx(SingleControlRepx(
            $"""<Item1 ControlType="DevExpress.XtraReports.UI.XRPictureBox, X" Name="pic" SizeF="100,100" LocationFloat="0,0" ImageSource="{pngBase64}" />"""));

        var el = Page(result.Design, "pic");
        Assert.Equal("image", el.Type);
        Assert.StartsWith("data:image/png;base64,", el.Content);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CANMIGDEVREP013");
    }
}
