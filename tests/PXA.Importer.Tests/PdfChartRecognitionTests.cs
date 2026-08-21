using PXA.FileImporter;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfChartRecognitionTests
{
    [Fact]
    public async Task ReviewMode_ReconstructsGeometricBarCandidate()
    {
        using var stream = CreateBarPdf(includeDataLabels: false);

        var design = await PdfFileImporter.DoImportAsync(stream, "Bar chart",
            new PdfFileImportOptions { ChartRecognition = PdfChartRecognitionMode.Review });

        var chart = Assert.Single(Assert.Single(design.Pages).Elements, element => element.Type == "chart");
        Assert.Equal("reviewRequired", chart.Chart!.Recognition!.Status);
        Assert.InRange(chart.Chart.Recognition.Confidence, 0.60, 0.849);
        Assert.Equal(3, chart.Chart.Series[0].Values.Count);
        Assert.Contains(design.ImportDiagnostics!, item => item.Code == "PXA-PDF-CHART-102");
    }

    [Fact]
    public async Task SafeMode_DoesNotReplaceUncertainGeometry()
    {
        using var stream = CreateBarPdf(includeDataLabels: false);

        var design = await PdfFileImporter.DoImportAsync(stream, "Uncertain bar chart",
            new PdfFileImportOptions { ChartRecognition = PdfChartRecognitionMode.Safe });

        Assert.DoesNotContain(Assert.Single(design.Pages).Elements, element => element.Type == "chart");
    }

    [Fact]
    public async Task SafeMode_ReconstructsFullyLabeledBarChartAutomatically()
    {
        using var stream = CreateBarPdf(includeDataLabels: true);

        var design = await PdfFileImporter.DoImportAsync(stream, "Labeled bar chart",
            new PdfFileImportOptions { ChartRecognition = PdfChartRecognitionMode.Safe });

        var chart = Assert.Single(Assert.Single(design.Pages).Elements, element => element.Type == "chart");
        Assert.Equal("automatic", chart.Chart!.Recognition!.Status);
        Assert.True(chart.Chart.Recognition.Confidence >= 0.85);
        Assert.Contains(design.ImportDiagnostics!, item => item.Code == "PXA-PDF-CHART-101");
    }

    [Fact]
    public async Task OffMode_LeavesOriginalPrimitivesUntouched()
    {
        using var stream = CreateBarPdf(includeDataLabels: true);

        var design = await PdfFileImporter.DoImportAsync(stream, "Disabled recognition",
            new PdfFileImportOptions { ChartRecognition = PdfChartRecognitionMode.Off });

        var elements = Assert.Single(design.Pages).Elements;
        Assert.DoesNotContain(elements, element => element.Type == "chart");
        Assert.True(elements.Count(element => element.Type == "shape") >= 3);
    }

    [Fact]
    public async Task ReviewMode_DoesNotClassifyUniformTableCellsAsChart()
    {
#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        var page = document.AddPage(400, 300);
        for (var index = 0; index < 5; index++)
            page.DrawRectangle(40 + index * 45, 100, 36, 24, fill: true, fillColor: PdfColor.FromRgb(240, 240, 240));
        using var stream = new MemoryStream(document.ToBytes());

        var design = await PdfFileImporter.DoImportAsync(stream, "Table",
            new PdfFileImportOptions { ChartRecognition = PdfChartRecognitionMode.Review });

        Assert.DoesNotContain(Assert.Single(design.Pages).Elements, element => element.Type == "chart");
    }

    private static MemoryStream CreateBarPdf(bool includeDataLabels)
    {
#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        var page = document.AddPage(400, 300);
        var heights = new[] { 40d, 85d, 62d };
        for (var index = 0; index < heights.Length; index++)
        {
            var x = 70 + index * 70;
            page.DrawRectangle(x, 80, 30, heights[index], fill: true,
                fillColor: PdfColor.FromRgb(37, 99, 235));
            page.DrawText($"Q{index + 1}", x + 6, 62, 9, PdfFontFamily.Helvetica);
            if (includeDataLabels)
                page.DrawText(((int)heights[index]).ToString(), x + 6, 84 + heights[index], 9, PdfFontFamily.Helvetica);
        }
        page.DrawLine(55, 80, 290, 80, 1, PdfColor.FromRgb(80, 80, 80));
        return new MemoryStream(document.ToBytes());
    }
}
