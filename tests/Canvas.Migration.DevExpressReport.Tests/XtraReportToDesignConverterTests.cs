using Canvas.Core.Contracts;
using Canvas.Migration.DevExpressReport;

namespace Canvas.Migration.DevExpressReport.Tests;

public sealed class XtraReportToDesignConverterTests
{
    // A representative designer-style report: a ReportHeader band (title) above a Detail band
    // (body label + line). Default ReportUnit (hundredths-of-inch → ×0.72), default 1-inch margins.
    private const string SampleReport = """
        using DevExpress.XtraReports.UI;
        using System.Drawing;

        public partial class InvoiceReport : XtraReport
        {
            private DetailBand Detail;
            private ReportHeaderBand ReportHeader;
            private XRLabel xrTitle;
            private XRLabel xrBody;
            private XRLine xrLine;

            private void InitializeComponent()
            {
                this.Detail = new DetailBand();
                this.ReportHeader = new ReportHeaderBand();
                this.xrTitle = new XRLabel();
                this.xrBody = new XRLabel();
                this.xrLine = new XRLine();

                this.ReportHeader.HeightF = 100F;
                this.Detail.HeightF = 300F;

                this.xrTitle.Text = "Invoice 2024";
                this.xrTitle.LocationF = new PointF(50F, 20F);
                this.xrTitle.SizeF = new SizeF(400F, 40F);
                this.xrTitle.Font = new Font("Tahoma", 18F, FontStyle.Bold);
                this.xrTitle.ForeColor = Color.Red;
                this.xrTitle.TextAlignment = TextAlignment.MiddleCenter;

                this.xrBody.Text = "Thank you";
                this.xrBody.LocationF = new PointF(50F, 30F);
                this.xrBody.SizeF = new SizeF(400F, 25F);

                this.xrLine.LocationF = new PointF(50F, 80F);
                this.xrLine.SizeF = new SizeF(400F, 2F);

                this.ReportHeader.Controls.AddRange(new XRControl[] { this.xrTitle });
                this.Detail.Controls.AddRange(new XRControl[] { this.xrBody, this.xrLine });
                this.Bands.AddRange(new Band[] { this.ReportHeader, this.Detail });
            }
        }
        """;

    private static ElementDto Element(DesignExportDto design, string name) =>
        design.Pages[0].Elements.Single(e => e.Name == name);

    [Fact]
    public void Convert_ProducesOnePageWithMappedElements()
    {
        var result = new XtraReportToDesignConverter().Convert(SampleReport);

        Assert.Equal("InvoiceReport", result.Design.Name);
        Assert.Single(result.Design.Pages);
        Assert.Equal(3, result.Design.Pages[0].Elements.Count);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP001");
    }

    [Fact]
    public void Convert_MapsLabelTextAndStyle()
    {
        var design = new XtraReportToDesignConverter().Convert(SampleReport).Design;
        var title = Element(design, "xrTitle");

        Assert.Equal("text", title.Type);
        Assert.Equal("Invoice 2024", title.Content);
        Assert.NotNull(title.Style);
        Assert.Equal(18d, System.Convert.ToDouble(title.Style!["fontSize"]));
        Assert.Equal("Tahoma", title.Style["fontFamily"]);
        Assert.Equal("bold", title.Style["fontWeight"]);
        Assert.Equal("center", title.Style["textAlign"]);
        Assert.Equal("#FF0000", title.Style["color"]);
    }

    [Fact]
    public void Convert_ConvertsUnitsAndFlattensBands()
    {
        var design = new XtraReportToDesignConverter().Convert(SampleReport).Design;

        // Title is in ReportHeader (band top = 100 units margin). x=(100+50)*0.72, y=(100+20)*0.72.
        var title = Element(design, "xrTitle");
        Assert.Equal(108d, title.X, 1);
        Assert.Equal(86.4d, title.Y, 1);
        Assert.Equal(288d, title.Width, 1);
        Assert.Equal(28.8d, title.Height, 1);

        // Body is in Detail, which starts after ReportHeader: band top = 100 + 100 = 200 units.
        // y = (200 + 30) * 0.72 = 165.6
        var body = Element(design, "xrBody");
        Assert.Equal(165.6d, body.Y, 1);

        // Line in Detail: y = (200 + 80) * 0.72 = 201.6
        var line = Element(design, "xrLine");
        Assert.Equal("line", line.Type);
        Assert.Equal(201.6d, line.Y, 1);
    }

    [Fact]
    public void Convert_SortsElementsTopToBottom()
    {
        var design = new XtraReportToDesignConverter().Convert(SampleReport).Design;
        var ys = design.Pages[0].Elements.Select(e => e.Y).ToList();

        Assert.Equal(ys.OrderBy(y => y), ys); // already sorted ascending by Y
    }

    [Fact]
    public void Convert_PixelsUnit_ScalesAt96Dpi()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel xrA;
                private void InitializeComponent()
                {
                    this.ReportUnit = ReportUnit.Pixels;
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.Detail = new DetailBand();
                    this.xrA = new XRLabel();
                    this.xrA.Text = "X";
                    this.xrA.LocationF = new PointF(100F, 0F);
                    this.xrA.SizeF = new SizeF(80F, 20F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrA });
                }
            }
            """;

        var design = new XtraReportToDesignConverter().Convert(source).Design;

        // 100 px × (72/96) = 75 pt
        Assert.Equal(75d, Element(design, "xrA").X, 1);
    }

    [Fact]
    public void Convert_PictureBox_BecomesImagePlaceholderWithWarning()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRPictureBox xrPic;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.xrPic = new XRPictureBox();
                    this.xrPic.LocationF = new PointF(0F, 0F);
                    this.xrPic.SizeF = new SizeF(100F, 100F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrPic });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);

        Assert.Equal("image", Element(result.Design, "xrPic").Type);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP013");
    }

    [Fact]
    public void Convert_UnsupportedControl_IsSkippedWithWarning()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRChart xrChart;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.xrChart = new XRChart();
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrChart });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);

        Assert.Empty(result.Design.Pages[0].Elements);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP011");
    }

    [Fact]
    public void Convert_XRTable_MapsRowsAndCellsToTableElement()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRTable xrTable;
                private XRTableRow xrRow1;
                private XRTableRow xrRow2;
                private XRTableCell xrCellName;
                private XRTableCell xrCellPrice;
                private XRTableCell xrCellA;
                private XRTableCell xrCellB;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.xrTable = new XRTable();
                    this.xrRow1 = new XRTableRow();
                    this.xrRow2 = new XRTableRow();
                    this.xrCellName = new XRTableCell();
                    this.xrCellPrice = new XRTableCell();
                    this.xrCellA = new XRTableCell();
                    this.xrCellB = new XRTableCell();

                    this.xrTable.LocationF = new PointF(0F, 0F);
                    this.xrTable.SizeF = new SizeF(400F, 50F);
                    this.xrCellName.Text = "Name";
                    this.xrCellPrice.Text = "Price";
                    this.xrCellA.Text = "Widget";
                    this.xrCellB.Text = "9.99";

                    this.xrRow1.Cells.AddRange(new XRTableCell[] { this.xrCellName, this.xrCellPrice });
                    this.xrRow2.Cells.AddRange(new XRTableCell[] { this.xrCellA, this.xrCellB });
                    this.xrTable.Rows.AddRange(new XRTableRow[] { this.xrRow1, this.xrRow2 });
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrTable });
                }
            }
            """;

        var design = new XtraReportToDesignConverter().Convert(source).Design;

        // Rows/cells are folded into one table element, not emitted standalone.
        Assert.Single(design.Pages[0].Elements);
        var table = Element(design, "xrTable");
        Assert.Equal("table", table.Type);
        Assert.NotNull(table.CellData);
        Assert.Equal(2, table.CellData!.Length);
        Assert.Equal(new[] { "Name", "Price" }, table.CellData[0]);
        Assert.Equal(new[] { "Widget", "9.99" }, table.CellData[1]);
        Assert.Equal(2, table.ColumnWidths!.Length);
    }

    [Fact]
    public void Convert_DataBoundControl_EmitsBindingWarning()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel xrTotal;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.xrTotal = new XRLabel();
                    this.xrTotal.Text = "0.00";
                    this.xrTotal.LocationF = new PointF(0F, 0F);
                    this.xrTotal.SizeF = new SizeF(100F, 20F);
                    this.xrTotal.ExpressionBindings.AddRange(new ExpressionBinding[] { new ExpressionBinding("Text", "[Total]") });
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrTotal });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);

        Assert.Equal("0.00", Element(result.Design, "xrTotal").Content);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP010");
    }
}
