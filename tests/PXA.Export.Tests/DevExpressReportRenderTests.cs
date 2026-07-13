using System.Text;
using PXA.Pdf;
using PXA.Migration.Report.Designer.DevExpress;
using PXA.WebApi.Infrastructure;

namespace PXA.Export.Tests;

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

    [Fact]
    public void LastPageScopedElement_RendersOnlyOnLastPage()
    {
        var design = new DesignExportDto
        {
            Id = "last-scope",
            Name = "Last page scope",
            PageSettings = new PageSettingsDto { Width = 300, Height = 300 },
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "footer",
                            Type = "text",
                            X = 20,
                            Y = 20,
                            Width = 160,
                            Height = 20,
                            Content = "Only last",
                            PageScope = "last"
                        }
                    ]
                },
                new PageDto
                {
                    Id = "p2",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "body",
                            Type = "text",
                            X = 20,
                            Y = 60,
                            Width = 160,
                            Height = 20,
                            Content = "Second page"
                        }
                    ]
                }
            ]
        };

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes(new PdfSaveOptions
        {
            CompressContentStreams = false
        });
        var pdf = Encoding.ASCII.GetString(bytes);

        Assert.Equal(1, CountOccurrences(pdf, "Only last"));
        Assert.Contains("Second page", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartElement_WithDotNetChartData_RendersToPdf()
    {
        var design = new DesignExportDto
        {
            Id = "chart-design",
            Name = "Chart Design",
            PageSettings = new PageSettingsDto { Width = 360, Height = 260 },
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "chart",
                            Type = "chart",
                            X = 20,
                            Y = 20,
                            Width = 300,
                            Height = 180,
                            ChartType = "bar",
                            ChartData = new Dictionary<string, object>
                            {
                                ["labels"] = new[] { "A", "B", "C" },
                                ["datasets"] = new object[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["label"] = "Revenue",
                                        ["data"] = new[] { 10, 30, 20 },
                                        ["backgroundColor"] = "#2563eb"
                                    }
                                }
                            }
                        }
                    ]
                }
            ]
        };

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes(new PdfSaveOptions
        {
            CompressContentStreams = false
        });

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1000, "PDF should contain a rendered chart image.");
    }

    [Fact]
    public void TextElement_WithPerSideBorders_RendersToValidPdf()
    {
        var design = new DesignExportDto
        {
            Id = "side-borders",
            Name = "Side borders",
            PageSettings = new PageSettingsDto { Width = 240, Height = 180 },
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto
                        {
                            Id = "box",
                            Type = "text",
                            X = 20,
                            Y = 20,
                            Width = 120,
                            Height = 36,
                            Content = "Top left",
                            Style = new Dictionary<string, object>
                            {
                                ["borderTopWidth"] = 2d,
                                ["borderTopColor"] = "#0000FF",
                                ["borderLeftWidth"] = 2d,
                                ["borderLeftColor"] = "#0000FF",
                                ["backgroundColor"] = "#FFFFFF"
                            }
                        }
                    ]
                }
            ]
        };

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 500, "PDF should contain rendered text and side borders.");
    }

    [Fact]
    public void VisibleExpression_LenCondition_FiltersElementWhenFalse()
    {
        var design = VisibleExpressionDesign("Len([Comment]) > 0", "Conditional comment");
        design.PageSettings!.CustomProperties =
        [
            new CustomDocumentPropertyDto { Name = "Comment", Value = "" }
        ];

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes(new PdfSaveOptions
        {
            CompressContentStreams = false
        });
        var pdf = Encoding.ASCII.GetString(bytes);

        Assert.DoesNotContain("Conditional comment", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleExpression_NestedFieldCondition_RendersElementWhenTrue()
    {
        var design = VisibleExpressionDesign("[RegionalCenter].[Company] == 'Galliker'", "Galliker logo");
        design.PageSettings!.CustomProperties =
        [
            new CustomDocumentPropertyDto { Name = "RegionalCenter.Company", Value = "Galliker" }
        ];

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes(new PdfSaveOptions
        {
            CompressContentStreams = false
        });
        var pdf = Encoding.ASCII.GetString(bytes);

        Assert.Contains("Galliker logo", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleExpression_IifCountCondition_FiltersElementWhenFalse()
    {
        var design = VisibleExpressionDesign("IIF([ExternalBookingFiles].Count > 0, True, False)", "External file");
        design.PageSettings!.CustomProperties =
        [
            new CustomDocumentPropertyDto { Name = "ExternalBookingFiles.Count", Value = "0" }
        ];

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes(new PdfSaveOptions
        {
            CompressContentStreams = false
        });
        var pdf = Encoding.ASCII.GetString(bytes);

        Assert.DoesNotContain("External file", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void RdlReportParameterDefault_SubstitutesAsCustomProperty()
    {
        var design = VisibleExpressionDesign("true", "Year {{OrderYear}}");
        design.PageSettings!.CustomProperties =
        [
            new CustomDocumentPropertyDto
            {
                Name = "rdlReportParameters",
                Value = """[{"Name":"OrderYear","DefaultValue":"2026"}]"""
            }
        ];

        var bytes = DesignJsonMapper.MapToPdfDocument(design).ToBytes(new PdfSaveOptions
        {
            CompressContentStreams = false
        });
        var pdf = Encoding.ASCII.GetString(bytes);

        Assert.Contains("Year 2026", pdf, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static DesignExportDto VisibleExpressionDesign(string visibleExpression, string content) => new()
    {
        Id = "visible-expression",
        Name = "Visible expression",
        PageSettings = new PageSettingsDto { Width = 240, Height = 180 },
        Pages =
        [
            new PageDto
            {
                Id = "p1",
                Elements =
                [
                    new ElementDto
                    {
                        Id = "conditional",
                        Type = "text",
                        X = 20,
                        Y = 20,
                        Width = 160,
                        Height = 30,
                        Content = content,
                        VisibleExpression = visibleExpression
                    }
                ]
            }
        ]
    };
}
