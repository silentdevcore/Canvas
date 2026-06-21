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
    public void Convert_LocationFloatPointFloat_IsParsed()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel xrA;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.Detail = new DetailBand();
                    this.xrA = new XRLabel();
                    this.xrA.Text = "A";
                    this.xrA.LocationFloat = new DevExpress.Utils.PointFloat(100F, 25F);
                    this.xrA.SizeF = new System.Drawing.SizeF(80F, 20F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrA });
                }
            }
            """;

        var design = new XtraReportToDesignConverter().Convert(source).Design;

        Assert.Equal(72d, Element(design, "xrA").X, 1);
        Assert.Equal(18d, Element(design, "xrA").Y, 1);
    }

    [Fact]
    public void Convert_ControlsAddRangeOrder_IsPreservedForOverlappingControls()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel front;
                private XRLabel back;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.Detail = new DetailBand();
                    this.front = new XRLabel();
                    this.back = new XRLabel();
                    this.front.Text = "Front";
                    this.front.LocationF = new PointF(0F, 0F);
                    this.front.SizeF = new SizeF(100F, 20F);
                    this.back.Text = "Back";
                    this.back.LocationF = new PointF(0F, 0F);
                    this.back.SizeF = new SizeF(100F, 20F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.back, this.front });
                }
            }
            """;

        var names = new XtraReportToDesignConverter()
            .Convert(source)
            .Design
            .Pages[0]
            .Elements
            .Select(e => e.Name ?? "")
            .ToArray();

        Assert.Equal(["back", "front"], names);
    }

    [Fact]
    public void Convert_BandsAddRangeOrder_IsUsedForRepeatedBandTypes()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private GroupHeaderBand groupHeader2;
                private GroupHeaderBand groupHeader1;
                private DetailBand Detail;
                private XRLabel label1;
                private XRLabel label2;
                private XRLabel detail;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.groupHeader2 = new GroupHeaderBand();
                    this.groupHeader1 = new GroupHeaderBand();
                    this.Detail = new DetailBand();
                    this.label1 = new XRLabel();
                    this.label2 = new XRLabel();
                    this.detail = new XRLabel();

                    this.groupHeader2.HeightF = 50F;
                    this.groupHeader1.HeightF = 60F;
                    this.Detail.HeightF = 100F;
                    this.label1.LocationF = new PointF(0F, 0F);
                    this.label1.SizeF = new SizeF(100F, 20F);
                    this.label2.LocationF = new PointF(0F, 0F);
                    this.label2.SizeF = new SizeF(100F, 20F);
                    this.detail.LocationF = new PointF(0F, 0F);
                    this.detail.SizeF = new SizeF(100F, 20F);

                    this.groupHeader1.Controls.AddRange(new XRControl[] { this.label1 });
                    this.groupHeader2.Controls.AddRange(new XRControl[] { this.label2 });
                    this.Detail.Controls.AddRange(new XRControl[] { this.detail });
                    this.Bands.AddRange(new Band[] { this.groupHeader1, this.groupHeader2, this.Detail });
                }
            }
            """;

        var design = new XtraReportToDesignConverter().Convert(source).Design;

        Assert.Equal(0d, Element(design, "label1").Y, 1);
        Assert.Equal(43.2d, Element(design, "label2").Y, 1);
        Assert.Equal(79.2d, Element(design, "detail").Y, 1);
    }

    [Fact]
    public void Convert_GroupBands_EmitGroupSemanticsDiagnostic()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraReports.UI.Sorting;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private GroupHeaderBand CustomerHeader;
                private GroupFooterBand CustomerFooter;
                private DetailBand Detail;
                private XRLabel header;
                private XRLabel footer;
                private XRLabel detail;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.CustomerHeader = new GroupHeaderBand();
                    this.CustomerFooter = new GroupFooterBand();
                    this.Detail = new DetailBand();
                    this.header = new XRLabel();
                    this.footer = new XRLabel();
                    this.detail = new XRLabel();

                    this.CustomerHeader.HeightF = 40F;
                    this.Detail.HeightF = 100F;
                    this.CustomerFooter.HeightF = 30F;
                    this.header.LocationF = new PointF(0F, 0F);
                    this.header.SizeF = new SizeF(100F, 20F);
                    this.footer.LocationF = new PointF(0F, 0F);
                    this.footer.SizeF = new SizeF(100F, 20F);
                    this.detail.LocationF = new PointF(0F, 0F);
                    this.detail.SizeF = new SizeF(100F, 20F);

                    this.CustomerHeader.GroupFields.AddRange(new GroupField[] { new GroupField("CustomerId", XRColumnSortOrder.Ascending) });
                    this.CustomerHeader.Controls.AddRange(new XRControl[] { this.header });
                    this.CustomerFooter.Controls.AddRange(new XRControl[] { this.footer });
                    this.Detail.Controls.AddRange(new XRControl[] { this.detail });
                    this.Bands.AddRange(new Band[] { this.CustomerHeader, this.Detail, this.CustomerFooter });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);

        Assert.Equal(0d, Element(result.Design, "header").Y, 1);
        Assert.Equal(28.8d, Element(result.Design, "detail").Y, 1);
        Assert.Equal(100.8d, Element(result.Design, "footer").Y, 1);
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "CANMIGDEVREP015" && d.Message.Contains("CustomerHeader", StringComparison.Ordinal));
        Assert.Contains("CustomerId", diagnostic.Message);
    }

    [Fact]
    public void Convert_TextLayoutHints_MapToStyleAndDiagnostics()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel notes;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.notes = new XRLabel();
                    this.notes.Text = "Line 1\nLine 2";
                    this.notes.LocationF = new PointF(0F, 0F);
                    this.notes.SizeF = new SizeF(200F, 40F);
                    this.notes.Multiline = true;
                    this.notes.WordWrap = true;
                    this.notes.CanGrow = true;
                    this.notes.CanShrink = true;
                    this.notes.KeepTogether = true;
                    this.notes.AnchorHorizontal = AnchorHorizontalStyles.Both;
                    this.notes.AnchorVertical = AnchorVerticalStyles.Bottom;
                    this.Detail.Controls.AddRange(new XRControl[] { this.notes });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var notes = Element(result.Design, "notes");

        Assert.Equal("pre-wrap", notes.Style!["whiteSpace"]);
        Assert.Equal("visible", notes.Style["overflow"]);
        Assert.Equal(true, notes.Style["devExpressCanShrink"]);
        Assert.Equal(true, notes.Style["devExpressKeepTogether"]);
        Assert.Equal("Both", notes.Style["devExpressAnchorHorizontal"]);
        Assert.Equal("Bottom", notes.Style["devExpressAnchorVertical"]);
        Assert.Equal("bottom", notes.Style["verticalAlign"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP016");
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP017");
    }

    [Fact]
    public void Convert_TextFitModeAndTextTrimming_MapToStyleMetadataAndDiagnostic()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.Drawing;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel notes;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.notes = new XRLabel();
                    this.notes.Text = "Long text";
                    this.notes.LocationF = new PointF(0F, 0F);
                    this.notes.SizeF = new SizeF(100F, 20F);
                    this.notes.TextFitMode = TextFitMode.ShrinkOnly;
                    this.notes.TextTrimming = DXStringTrimming.Word;
                    this.Detail.Controls.AddRange(new XRControl[] { this.notes });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var style = Element(result.Design, "notes").Style!;

        Assert.Equal("ShrinkOnly", style["devExpressTextFitMode"]);
        Assert.Equal("Word", style["devExpressTextTrimming"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP023");
    }

    [Fact]
    public void Convert_DetailBandMultiColumn_EmitsDiagnostic()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel item;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.item = new XRLabel();
                    this.item.Text = "Item";
                    this.item.LocationF = new PointF(0F, 0F);
                    this.item.SizeF = new SizeF(100F, 20F);
                    this.Detail.MultiColumn.Mode = MultiColumnMode.UseColumnCount;
                    this.Detail.Controls.AddRange(new XRControl[] { this.item });
                    this.Bands.AddRange(new Band[] { this.Detail });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);

        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP022" && d.Message.Contains("UseColumnCount", StringComparison.Ordinal));
    }

    [Fact]
    public void Convert_ControlStyleName_AppliesFontColorAndPadding()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraPrinting;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRControlStyle DataBoundText;
                private XRLabel value;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.DataBoundText = new XRControlStyle();
                    this.value = new XRLabel();
                    this.DataBoundText.Name = "DataBoundText";
                    this.DataBoundText.Font = new Font("Arial", 11F, FontStyle.Bold);
                    this.DataBoundText.ForeColor = Color.Blue;
                    this.DataBoundText.Padding = new PaddingInfo(2, 3, 4, 5, 100F);
                    this.value.StyleName = "DataBoundText";
                    this.value.Text = "Styled";
                    this.value.LocationF = new PointF(0F, 0F);
                    this.value.SizeF = new SizeF(100F, 20F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.value });
                    this.StyleSheet.AddRange(new XRControlStyle[] { this.DataBoundText });
                }
            }
            """;

        var style = Element(new XtraReportToDesignConverter().Convert(source).Design, "value").Style!;

        Assert.Equal("Arial", style["fontFamily"]);
        Assert.Equal(11d, System.Convert.ToDouble(style["fontSize"]));
        Assert.Equal("bold", style["fontWeight"]);
        Assert.Equal("#0000FF", style["color"]);
        Assert.Equal(2d, System.Convert.ToDouble(style["paddingLeft"]));
        Assert.Equal(3d, System.Convert.ToDouble(style["paddingRight"]));
        Assert.Equal(4d, System.Convert.ToDouble(style["paddingTop"]));
        Assert.Equal(5d, System.Convert.ToDouble(style["paddingBottom"]));
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
    public void Convert_XRChart_BecomesCanvasChartPlaceholder()
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
                    this.xrChart.LocationF = new PointF(10F, 20F);
                    this.xrChart.SizeF = new SizeF(300F, 160F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrChart });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var chart = Element(result.Design, "xrChart");

        Assert.Equal("chart", chart.Type);
        Assert.Equal("bar", chart.ChartType);
        Assert.NotNull(chart.ChartData);
        Assert.Equal(79.2d, chart.X, 1);
        Assert.Equal(86.4d, chart.Y, 1);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP018");
    }

    [Fact]
    public void Convert_XRGaugeAndXRPivotGrid_BecomePositionedPlaceholders()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRGauge gauge;
                private XRPivotGrid pivot;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.Detail = new DetailBand();
                    this.gauge = new XRGauge();
                    this.pivot = new XRPivotGrid();
                    this.gauge.LocationF = new PointF(0F, 0F);
                    this.gauge.SizeF = new SizeF(120F, 80F);
                    this.pivot.LocationF = new PointF(0F, 100F);
                    this.pivot.SizeF = new SizeF(240F, 120F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.gauge, this.pivot });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);

        var gauge = Element(result.Design, "gauge");
        var pivot = Element(result.Design, "pivot");
        Assert.Equal("text", gauge.Type);
        Assert.Contains("Gauge", gauge.Content);
        Assert.Equal("text", pivot.Type);
        Assert.Contains("PivotGrid", pivot.Content);
        Assert.Equal(72d, pivot.Y, 1);
        Assert.Equal(2, result.Diagnostics.Count(d => d.Id == "CANMIGDEVREP018"));
    }

    private static string ReportWithPageSetup(string setup) => $$"""
        using DevExpress.XtraReports.UI;
        using System.Drawing;
        using System.Drawing.Printing;
        public partial class R : XtraReport
        {
            private DetailBand Detail;
            private XRLabel xrA;
            private void InitializeComponent()
            {
                {{setup}}
                this.Detail = new DetailBand();
                this.xrA = new XRLabel();
                this.xrA.Text = "X";
                this.xrA.LocationF = new PointF(0F, 0F);
                this.xrA.SizeF = new SizeF(50F, 20F);
                this.Detail.Controls.AddRange(new XRControl[] { this.xrA });
            }
        }
        """;

    [Theory]
    [InlineData("this.PaperKind = PaperKind.A4;", 595, 842)]
    [InlineData("this.PaperKind = PaperKind.Letter;", 612, 792)]
    [InlineData("this.PaperKind = PaperKind.Legal;", 612, 1008)]
    [InlineData("this.PaperKind = PaperKind.A3;", 842, 1191)]
    public void Convert_PaperKind_SetsPageSize(string setup, double width, double height)
    {
        var design = new XtraReportToDesignConverter().Convert(ReportWithPageSetup(setup)).Design;

        Assert.Equal(width, design.PageSettings!.Width, 1);
        Assert.Equal(height, design.PageSettings.Height, 1);
    }

    [Fact]
    public void Convert_CustomPaperSize_UsesPageWidthHeightInReportUnits()
    {
        // 850 × 1100 hundredths-of-inch × 0.72 = 612 × 792 pt (US Letter).
        var design = new XtraReportToDesignConverter()
            .Convert(ReportWithPageSetup("this.PaperKind = PaperKind.Custom; this.PageWidth = 850; this.PageHeight = 1100;"))
            .Design;

        Assert.Equal(612d, design.PageSettings!.Width, 1);
        Assert.Equal(792d, design.PageSettings.Height, 1);
    }

    [Fact]
    public void Convert_Landscape_SwapsWidthAndHeight()
    {
        var design = new XtraReportToDesignConverter()
            .Convert(ReportWithPageSetup("this.PaperKind = PaperKind.A4; this.Landscape = true;"))
            .Design;

        Assert.Equal(842d, design.PageSettings!.Width, 1);
        Assert.Equal(595d, design.PageSettings.Height, 1);
    }

    [Fact]
    public void Convert_PageHeaderAndFooter_BecomeSharedElements()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private PageHeaderBand PageHeader;
                private PageFooterBand PageFooter;
                private DetailBand Detail;
                private XRLabel xrHeader;
                private XRLabel xrFooter;
                private XRLabel xrBody;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.PageHeader = new PageHeaderBand();
                    this.PageFooter = new PageFooterBand();
                    this.Detail = new DetailBand();
                    this.xrHeader = new XRLabel();
                    this.xrFooter = new XRLabel();
                    this.xrBody = new XRLabel();

                    this.PageHeader.HeightF = 50F;
                    this.PageFooter.HeightF = 40F;
                    this.Detail.HeightF = 200F;

                    this.xrHeader.Text = "Header";
                    this.xrHeader.LocationF = new PointF(0F, 10F);
                    this.xrHeader.SizeF = new SizeF(200F, 20F);
                    this.xrFooter.Text = "Footer";
                    this.xrFooter.LocationF = new PointF(0F, 5F);
                    this.xrFooter.SizeF = new SizeF(200F, 20F);
                    this.xrBody.Text = "Body";
                    this.xrBody.LocationF = new PointF(0F, 10F);
                    this.xrBody.SizeF = new SizeF(200F, 20F);

                    this.PageHeader.Controls.AddRange(new XRControl[] { this.xrHeader });
                    this.PageFooter.Controls.AddRange(new XRControl[] { this.xrFooter });
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrBody });
                }
            }
            """;

        var design = new XtraReportToDesignConverter().Convert(source).Design;

        // Header + footer repeat → shared; body stays on the page.
        Assert.Equal(2, design.SharedElements.Count);
        Assert.Single(design.Pages[0].Elements);
        Assert.Equal("xrBody", design.Pages[0].Elements[0].Name);

        var header = design.SharedElements.Single(e => e.Name == "xrHeader");
        Assert.Equal(7.2d, header.Y, 1); // near the top

        var footer = design.SharedElements.Single(e => e.Name == "xrFooter");
        Assert.True(footer.Y > 800, $"footer should be anchored near the A4 bottom, was {footer.Y}");
    }

    [Fact]
    public void Convert_ReportFooterBand_IsScopedToLastPage()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private ReportFooterBand ReportFooter;
                private XRLabel body;
                private XRLabel total;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.Detail = new DetailBand();
                    this.ReportFooter = new ReportFooterBand();
                    this.body = new XRLabel();
                    this.total = new XRLabel();

                    this.Detail.HeightF = 100F;
                    this.ReportFooter.HeightF = 40F;

                    this.body.Text = "Body";
                    this.body.LocationF = new PointF(0F, 0F);
                    this.body.SizeF = new SizeF(100F, 20F);

                    this.total.Text = "Grand total";
                    this.total.LocationF = new PointF(0F, 10F);
                    this.total.SizeF = new SizeF(100F, 20F);

                    this.Detail.Controls.AddRange(new XRControl[] { this.body });
                    this.ReportFooter.Controls.AddRange(new XRControl[] { this.total });
                    this.Bands.AddRange(new Band[] { this.Detail, this.ReportFooter });
                }
            }
            """;

        var design = new XtraReportToDesignConverter().Convert(source).Design;

        var total = Element(design, "total");
        Assert.Equal("last", total.PageScope);
        Assert.Equal(79.2d, total.Y, 1); // (Detail 100 + local 10) * 0.72
    }

    [Fact]
    public void Convert_DetailReportBand_StacksNestedDetailBands()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private DetailReportBand linesReport;
                private DetailBand linesDetail;
                private XRLabel body;
                private XRLabel line;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.Detail = new DetailBand();
                    this.linesReport = new DetailReportBand();
                    this.linesDetail = new DetailBand();
                    this.body = new XRLabel();
                    this.line = new XRLabel();

                    this.Detail.HeightF = 100F;
                    this.linesReport.HeightF = 20F;
                    this.linesDetail.HeightF = 30F;

                    this.body.Text = "Body";
                    this.body.LocationF = new PointF(0F, 0F);
                    this.body.SizeF = new SizeF(100F, 20F);

                    this.line.Text = "Line";
                    this.line.LocationF = new PointF(0F, 5F);
                    this.line.SizeF = new SizeF(100F, 20F);

                    this.Detail.Controls.AddRange(new XRControl[] { this.body });
                    this.linesDetail.Controls.AddRange(new XRControl[] { this.line });
                    this.linesReport.Bands.AddRange(new Band[] { this.linesDetail });
                    this.Bands.AddRange(new Band[] { this.Detail, this.linesReport });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);

        Assert.Equal(0d, Element(result.Design, "body").Y, 1);
        Assert.Equal(90d, Element(result.Design, "line").Y, 1); // (Detail 100 + DetailReport 20 + local 5) * 0.72
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP014");
    }

    [Fact]
    public void Convert_NestedPanelControls_AreFlattened()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRPanel panel;
                private XRLabel inner;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.panel = new XRPanel();
                    this.inner = new XRLabel();
                    this.panel.LocationF = new PointF(50F, 30F);
                    this.panel.SizeF = new SizeF(200F, 80F);
                    this.inner.Text = "Inside";
                    this.inner.LocationF = new PointF(10F, 5F);
                    this.inner.SizeF = new SizeF(100F, 20F);
                    this.panel.Controls.AddRange(new XRControl[] { this.inner });
                    this.Detail.Controls.AddRange(new XRControl[] { this.panel });
                }
            }
            """;

        var design = new XtraReportToDesignConverter().Convert(source).Design;

        var inner = Element(design, "inner");
        Assert.Equal(115.2d, inner.X, 1); // (100 margin + 50 panel + 10) * 0.72
        Assert.Equal(97.2d, inner.Y, 1);  // (100 margin + 30 panel + 5) * 0.72
    }

    [Fact]
    public void Convert_XRCheckBoxAndXRShape_MapToCheckmarkAndCircle()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraPrinting.Shape;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRCheckBox chk;
                private XRShape shp;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.chk = new XRCheckBox();
                    this.shp = new XRShape();
                    this.chk.Text = "Agree";
                    this.chk.CheckBoxState = CheckBoxState.Checked;
                    this.chk.LocationF = new PointF(0F, 0F);
                    this.chk.SizeF = new SizeF(100F, 20F);
                    this.shp.Shape = new ShapeEllipse();
                    this.shp.LocationF = new PointF(0F, 40F);
                    this.shp.SizeF = new SizeF(60F, 60F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.chk, this.shp });
                }
            }
            """;

        var design = new XtraReportToDesignConverter().Convert(source).Design;

        var chk = Element(design, "chk");
        Assert.Equal("checkmark", chk.Type);
        Assert.Equal("checked", chk.CheckState);

        Assert.Equal("circle", Element(design, "shp").Type);
    }

    [Fact]
    public void Convert_XRShapeArrow_MapsToCanvasArrowWithDiagnostic()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraPrinting.Shape;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRShape arrow;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.arrow = new XRShape();
                    this.arrow.Shape = new ShapeArrow();
                    this.arrow.BorderColor = Color.Red;
                    this.arrow.BorderWidth = 2F;
                    this.arrow.LocationF = new PointF(0F, 0F);
                    this.arrow.SizeF = new SizeF(120F, 20F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.arrow });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var arrow = Element(result.Design, "arrow");

        Assert.Equal("arrow", arrow.Type);
        Assert.Equal("arrow", arrow.EndMarker);
        Assert.Equal("#FF0000", arrow.Style!["color"]);
        Assert.Equal(2d, System.Convert.ToDouble(arrow.Style["strokeWidth"]));
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP019");
    }

    [Fact]
    public void Convert_LabelBorders_MapPerSideBorderStyle()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraPrinting;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel box;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.box = new XRLabel();
                    this.box.Text = "Bordered";
                    this.box.Borders = BorderSide.Left | BorderSide.Top;
                    this.box.BorderColor = Color.Blue;
                    this.box.BorderWidth = 2F;
                    this.box.LocationF = new PointF(0F, 0F);
                    this.box.SizeF = new SizeF(100F, 20F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.box });
                }
            }
            """;

        var style = Element(new XtraReportToDesignConverter().Convert(source).Design, "box").Style!;

        Assert.Equal(2d, System.Convert.ToDouble(style["borderLeftWidth"]));
        Assert.Equal(2d, System.Convert.ToDouble(style["borderTopWidth"]));
        Assert.Equal("#0000FF", style["borderLeftColor"]);
        Assert.Equal("#0000FF", style["borderTopColor"]);
        Assert.False(style.ContainsKey("borderRightWidth"));
    }

    [Fact]
    public void Convert_LabelBordersNone_DisablesBorder()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraPrinting;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel box;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.box = new XRLabel();
                    this.box.Text = "No border";
                    this.box.Borders = BorderSide.None;
                    this.box.BorderWidth = 2F;
                    this.box.LocationF = new PointF(0F, 0F);
                    this.box.SizeF = new SizeF(100F, 20F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.box });
                }
            }
            """;

        var style = Element(new XtraReportToDesignConverter().Convert(source).Design, "box").Style!;

        Assert.Equal(0d, System.Convert.ToDouble(style["borderWidth"]));
    }

    [Fact]
    public void Convert_XRLine_MapsColorStrokeAndHorizontalGeometry()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraPrinting;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLine rule;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.Detail = new DetailBand();
                    this.rule = new XRLine();
                    this.rule.ForeColor = Color.Red;
                    this.rule.LineDirection = LineDirection.Horizontal;
                    this.rule.LineWidth = 2F;
                    this.rule.LineStyle = DashStyle.Dash;
                    this.rule.LocationF = new PointF(10F, 20F);
                    this.rule.SizeF = new SizeF(200F, 10F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.rule });
                }
            }
            """;

        var line = Element(new XtraReportToDesignConverter().Convert(source).Design, "rule");

        Assert.Equal("line", line.Type);
        Assert.Equal(2d, System.Convert.ToDouble(line.Style!["strokeWidth"]));
        Assert.Equal("#FF0000", line.Style["color"]);
        Assert.Equal("#FF0000", line.Style["backgroundColor"]);
        Assert.Equal("dashed", line.Style["dashStyle"]);
        Assert.Equal("horizontal", line.Style["lineDirection"]);
        Assert.Equal(2d, line.Height, 1);
        Assert.Equal(17d, line.Y, 1);       // centered in original 10-unit-tall line box
    }

    [Fact]
    public void Convert_XRLine_LineDirectionVertical_MapsToThinVerticalBox()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLine rule;
                private void InitializeComponent()
                {
                    this.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);
                    this.Detail = new DetailBand();
                    this.rule = new XRLine();
                    this.rule.LineDirection = LineDirection.Vertical;
                    this.rule.LineWidth = 3F;
                    this.rule.LocationF = new PointF(10F, 20F);
                    this.rule.SizeF = new SizeF(30F, 120F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.rule });
                }
            }
            """;

        var line = Element(new XtraReportToDesignConverter().Convert(source).Design, "rule");

        Assert.Equal("vertical", line.Style!["lineDirection"]);
        Assert.Equal(3d, line.Width, 1);
        Assert.Equal(16.5d, line.X, 1);    // centered in original 30-unit-wide line box
        Assert.Equal(86.4d, line.Height, 1);
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
    public void Convert_SingleFieldBinding_MapsToCanvasBinding()
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
                    this.xrTotal.ExpressionBindings.AddRange(new ExpressionBinding[] { new ExpressionBinding("BeforePrint", "Text", "[Total]") });
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrTotal });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var el = Element(result.Design, "xrTotal");

        Assert.Equal("Total", el.Binding);
        Assert.Equal("{{Total}}", el.Content);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP010");
    }

    [Fact]
    public void Convert_BarCodeTextBinding_MapsToBarcodeValue()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRBarCode sku;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.sku = new XRBarCode();
                    this.sku.Text = "fallback";
                    this.sku.LocationF = new PointF(0F, 0F);
                    this.sku.SizeF = new SizeF(200F, 60F);
                    this.sku.ExpressionBindings.AddRange(new ExpressionBinding[] { new ExpressionBinding("BeforePrint", "Text", "[Sku]") });
                    this.Detail.Controls.AddRange(new XRControl[] { this.sku });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var el = Element(result.Design, "sku");

        Assert.Equal("barcode", el.Type);
        Assert.Equal("Sku", el.Binding);
        Assert.Equal("{{Sku}}", el.BarcodeValue);
        Assert.DoesNotContain(result.Diagnostics, d =>
            d.Id == "CANMIGDEVREP010"
            && d.Severity == Canvas.Migration.Abstractions.MigrationDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Convert_PictureBoxImageSourceBinding_MapsToImageContentPlaceholder()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRPictureBox logo;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.logo = new XRPictureBox();
                    this.logo.LocationF = new PointF(0F, 0F);
                    this.logo.SizeF = new SizeF(100F, 100F);
                    this.logo.ExpressionBindings.AddRange(new ExpressionBinding[] { new ExpressionBinding("BeforePrint", "ImageSource", "[LogoDataUrl]") });
                    this.Detail.Controls.AddRange(new XRControl[] { this.logo });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var el = Element(result.Design, "logo");

        Assert.Equal("image", el.Type);
        Assert.Equal("LogoDataUrl", el.Binding);
        Assert.Equal("{{LogoDataUrl}}", el.Content);
    }

    [Fact]
    public void Convert_PictureBoxResourceImageSource_PreservesResourceKeyWithDiagnostic()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraPrinting.Drawing;
            using System.ComponentModel;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRPictureBox logo;
                private void InitializeComponent()
                {
                    ComponentResourceManager resources = new ComponentResourceManager(typeof(R));
                    this.Detail = new DetailBand();
                    this.logo = new XRPictureBox();
                    this.logo.ImageSource = new ImageSource("img", resources.GetString("logo.ImageSource"));
                    this.logo.LocationF = new PointF(0F, 0F);
                    this.logo.SizeF = new SizeF(100F, 100F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.logo });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var logo = Element(result.Design, "logo");

        Assert.Equal("image", logo.Type);
        Assert.Equal("logo.ImageSource", logo.Style!["devExpressImageResourceKey"]);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP021");
    }

    [Fact]
    public void Convert_PictureBoxResourceImageSource_WithResourceMap_EmbedsDataUrl()
    {
        const string pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraPrinting.Drawing;
            using System.ComponentModel;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRPictureBox logo;
                private void InitializeComponent()
                {
                    ComponentResourceManager resources = new ComponentResourceManager(typeof(R));
                    this.Detail = new DetailBand();
                    this.logo = new XRPictureBox();
                    this.logo.ImageSource = new ImageSource("img", resources.GetString("logo.ImageSource"));
                    this.logo.LocationF = new PointF(0F, 0F);
                    this.logo.SizeF = new SizeF(100F, 100F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.logo });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(
            source,
            new Dictionary<string, string> { ["logo.ImageSource"] = pngBase64 });
        var logo = Element(result.Design, "logo");

        Assert.Equal("image", logo.Type);
        Assert.Equal($"data:image/png;base64,{pngBase64}", logo.Content);
        Assert.Equal("logo.ImageSource", logo.Style!["devExpressImageResourceKey"]);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CANMIGDEVREP021");
    }

    [Fact]
    public void Convert_ResourceExpressionBinding_ResolvesTextExpression()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.ComponentModel;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel customer;
                private void InitializeComponent()
                {
                    ComponentResourceManager resources = new ComponentResourceManager(typeof(R));
                    this.Detail = new DetailBand();
                    this.customer = new XRLabel();
                    this.customer.LocationF = new PointF(0F, 0F);
                    this.customer.SizeF = new SizeF(100F, 20F);
                    this.customer.ExpressionBindings.AddRange(new ExpressionBinding[] {
                        new ExpressionBinding("BeforePrint", "Text", resources.GetString("customer.ExpressionBindings")) });
                    this.Detail.Controls.AddRange(new XRControl[] { this.customer });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(
            source,
            new Dictionary<string, string> { ["customer.ExpressionBindings"] = "[CustomerName]" });
        var customer = Element(result.Design, "customer");

        Assert.Equal("text", customer.Type);
        Assert.Equal("CustomerName", customer.Binding);
        Assert.Equal("{{CustomerName}}", customer.Content);
    }

    [Fact]
    public void ParseResx_ReturnsNamedValues()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="logo.ImageSource" xml:space="preserve">
                <value>img|iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=</value>
              </data>
              <data name="customer.ExpressionBindings" xml:space="preserve">
                <value>[CustomerName]</value>
              </data>
            </root>
            """;

        var resources = DevExpressReportResourceParser.ParseResx(resx);

        Assert.Equal("img|iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=", resources["logo.ImageSource"]);
        Assert.Equal("[CustomerName]", resources["customer.ExpressionBindings"]);
    }

    [Fact]
    public void Convert_WithParsedResx_EmbedsImageAndResolvesBinding()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using DevExpress.XtraPrinting.Drawing;
            using System.ComponentModel;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRPictureBox logo;
                private XRLabel customer;
                private void InitializeComponent()
                {
                    ComponentResourceManager resources = new ComponentResourceManager(typeof(R));
                    this.Detail = new DetailBand();
                    this.logo = new XRPictureBox();
                    this.customer = new XRLabel();
                    this.logo.ImageSource = new ImageSource("img", resources.GetString("logo.ImageSource"));
                    this.logo.LocationF = new PointF(0F, 0F);
                    this.logo.SizeF = new SizeF(100F, 100F);
                    this.customer.LocationF = new PointF(0F, 110F);
                    this.customer.SizeF = new SizeF(100F, 20F);
                    this.customer.ExpressionBindings.AddRange(new ExpressionBinding[] {
                        new ExpressionBinding("BeforePrint", "Text", resources.GetString("customer.ExpressionBindings")) });
                    this.Detail.Controls.AddRange(new XRControl[] { this.logo, this.customer });
                }
            }
            """;
        var resx = """
            <root>
              <data name="logo.ImageSource" xml:space="preserve">
                <value>img|iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=</value>
              </data>
              <data name="customer.ExpressionBindings" xml:space="preserve">
                <value>[CustomerName]</value>
              </data>
            </root>
            """;

        var result = new XtraReportToDesignConverter().Convert(
            source,
            DevExpressReportResourceParser.ParseResx(resx));

        var logo = Element(result.Design, "logo");
        var customer = Element(result.Design, "customer");
        Assert.StartsWith("data:image/png;base64,", logo.Content);
        Assert.Equal("CustomerName", customer.Binding);
        Assert.Equal("{{CustomerName}}", customer.Content);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CANMIGDEVREP021");
    }

    [Fact]
    public void Convert_XRSubreport_BecomesPositionedPlaceholderWithDiagnostic()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRSubreport sub;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.sub = new XRSubreport();
                    this.sub.LocationF = new PointF(20F, 30F);
                    this.sub.SizeF = new SizeF(200F, 80F);
                    this.Detail.Controls.AddRange(new XRControl[] { this.sub });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var sub = Element(result.Design, "sub");

        Assert.Equal("subsection", sub.Type);
        Assert.Contains("Subreport", sub.Content);
        Assert.Equal(86.4d, sub.X, 1);
        Assert.Equal(93.6d, sub.Y, 1);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP012");
    }

    [Fact]
    public void Convert_VisibleExpressionBinding_MapsToVisibleExpression()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel comment;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.comment = new XRLabel();
                    this.comment.Text = "Comment";
                    this.comment.LocationF = new PointF(0F, 0F);
                    this.comment.SizeF = new SizeF(100F, 20F);
                    this.comment.ExpressionBindings.AddRange(new ExpressionBinding[] { new ExpressionBinding("BeforePrint", "Visible", "Len([Comment]) > 0") });
                    this.Detail.Controls.AddRange(new XRControl[] { this.comment });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var comment = Element(result.Design, "comment");

        Assert.Equal("Len([Comment]) > 0", comment.VisibleExpression);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP020");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CANMIGDEVREP010" && d.Message.Contains("Visible", StringComparison.Ordinal));
    }

    [Fact]
    public void Convert_ComplexExpression_MapsToExpressionField()
    {
        var source = """
            using DevExpress.XtraReports.UI;
            using System.Drawing;
            public partial class R : XtraReport
            {
                private DetailBand Detail;
                private XRLabel xrAmount;
                private void InitializeComponent()
                {
                    this.Detail = new DetailBand();
                    this.xrAmount = new XRLabel();
                    this.xrAmount.LocationF = new PointF(0F, 0F);
                    this.xrAmount.SizeF = new SizeF(100F, 20F);
                    this.xrAmount.ExpressionBindings.AddRange(new ExpressionBinding[] { new ExpressionBinding("BeforePrint", "Text", "[Qty] * [Price]") });
                    this.Detail.Controls.AddRange(new XRControl[] { this.xrAmount });
                }
            }
            """;

        var result = new XtraReportToDesignConverter().Convert(source);
        var el = Element(result.Design, "xrAmount");

        Assert.Equal("[Qty] * [Price]", el.Expression);
        Assert.Contains(result.Diagnostics, d => d.Id == "CANMIGDEVREP010" && d.Severity == Canvas.Migration.Abstractions.MigrationDiagnosticSeverity.Warning);
    }
}
