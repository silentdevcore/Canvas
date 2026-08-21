using System.Text;
using System.Diagnostics;
using PXA.Core.Contracts;
using PXA.Infrastructure.Pdf.Charts;
using PXA.Pdf;

namespace PXA.Infrastructure.Pdf.Tests;

public sealed class ChartPdfRendererTests
{
    [Theory]
    [InlineData(PxaChartTypes.Bar)]
    [InlineData(PxaChartTypes.Line)]
    [InlineData(PxaChartTypes.Area)]
    [InlineData(PxaChartTypes.Pie)]
    [InlineData(PxaChartTypes.Doughnut)]
    [InlineData(PxaChartTypes.StackedBar)]
    [InlineData(PxaChartTypes.Combo)]
    public void Render_UsesVectorOutput_ForCoreChartTypes(string chartType)
    {
#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        var page = document.AddPage(595, 842);
        var element = CreateChart(chartType);

        var result = new PxaChartPdfRenderer().Render(page, element, 40, 400, 480, 260);
        var bytes = document.ToBytes();
        var pdf = Encoding.Latin1.GetString(bytes);

        Assert.Equal(ChartRenderMode.Vector, result.Mode);
        Assert.StartsWith("%PDF-", pdf);
        Assert.DoesNotContain("/Subtype /Image", pdf, StringComparison.Ordinal);
        Assert.Contains("Quarterly result", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ProducesDiagnosedEmptyState_WithoutDemoData()
    {
#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        var page = document.AddPage(595, 842);
        var element = new ElementDto
        {
            Id = "empty-chart",
            Type = "chart",
            Chart = new ChartDefinitionDto { Categories = ["A"] }
        };

        var result = new PxaChartPdfRenderer().Render(page, element, 40, 400, 480, 260);
        var pdf = Encoding.Latin1.GetString(document.ToBytes());

        Assert.Equal(ChartRenderMode.Empty, result.Mode);
        Assert.StartsWith("PXACHART001", result.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("No chart data", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_FormatsDataLabelsWithChartLocale()
    {
#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        var page = document.AddPage(595, 842);
        var element = CreateChart(PxaChartTypes.Bar);
        element.Chart!.Locale = "de-DE";
        element.Chart.Series[0].Values = [1.5, 2.5, 3.5, 4.5];

        new PxaChartPdfRenderer().Render(page, element, 40, 400, 480, 260);
        var pdf = Encoding.Latin1.GetString(document.ToBytes());

        Assert.Contains("1,5", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_MaximumSupportedPointCount_RemainsBounded()
    {
#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        var page = document.AddPage(595, 842);
        var element = new ElementDto
        {
            Id = "bounded-chart",
            Type = "chart",
            Chart = new ChartDefinitionDto
            {
                Type = PxaChartTypes.Line,
                Categories = Enumerable.Range(1, 6_000).Select(index => $"P{index}").ToList(),
                Series =
                [
                    new ChartSeriesDto
                    {
                        Id = "series-1",
                        Name = "Bounded series",
                        Values = Enumerable.Range(1, 6_000).Select(index => (double?)(index % 101)).ToList(),
                        ShowMarkers = false,
                    }
                ],
            },
        };
        var stopwatch = Stopwatch.StartNew();

        var result = new PxaChartPdfRenderer().Render(page, element, 40, 400, 480, 260);
        var bytes = document.ToBytes();
        stopwatch.Stop();

        Assert.Equal(ChartRenderMode.Vector, result.Mode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Chart rendering took {stopwatch.Elapsed}.");
        Assert.InRange(bytes.Length, 100, 10_000_000);
    }

    private static ElementDto CreateChart(string chartType) => new()
    {
        Id = $"{chartType}-chart",
        Type = "chart",
        Chart = new ChartDefinitionDto
        {
            Type = chartType,
            Title = "Quarterly result",
            Categories = ["Q1", "Q2", "Q3", "Q4"],
            DataLabels = new ChartDataLabelsDto { Visible = true },
            Series =
            [
                new ChartSeriesDto { Id = "revenue", Name = "Revenue", Values = [12, -4, 18, 22] },
                new ChartSeriesDto
                {
                    Id = "margin", Name = "Margin", Type = chartType == PxaChartTypes.Combo ? PxaChartTypes.Line : null,
                    Values = [4, null, 8, 9], Color = "#16a34a"
                }
            ]
        }
    };
}
