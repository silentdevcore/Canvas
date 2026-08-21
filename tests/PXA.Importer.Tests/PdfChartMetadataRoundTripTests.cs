using PXA.Core.Contracts;
using PXA.Core.Primitives;
using PXA.FileImporter;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfChartMetadataRoundTripTests
{
    [Fact]
    public async Task Import_RestoresPxaChartMetadata_AndConsumesRenderedPrimitives()
    {
        var chart = new ElementDto
        {
            Id = "sales-chart", Type = "chart", X = 40, Y = 60, Width = 300, Height = 180,
            Chart = new ChartDefinitionDto
            {
                Type = PxaChartTypes.Bar,
                Categories = ["A", "B", "C"],
                Series = [new ChartSeriesDto { Id = "sales", Name = "Sales", Values = [4, 8, 6] }]
            }
        };
        var encoded = ChartMetadataCodec.Encode([new PlannedPage("page-1", [chart])]);

#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        document.Info.CustomProperties[ChartMetadataCodec.PdfInfoKey] = Assert.IsType<string>(encoded);
        var page = document.AddPage(595, 842);
        page.DrawRectangle(40, 602, 300, 180, fill: true, fillColor: PdfColor.FromRgb(37, 99, 235));
        page.DrawText("Outside chart", 40, 500, 12, PdfFontFamily.Helvetica);

        using var stream = new MemoryStream(document.ToBytes());
        var imported = await PdfFileImporter.DoImportAsync(stream, "Chart roundtrip");

        var elements = Assert.Single(imported.Pages).Elements;
        var restored = Assert.Single(elements, element => element.Type == "chart");
        Assert.Equal("sales-chart", restored.Id);
        Assert.Equal("automatic", restored.Chart!.Recognition!.Status);
        Assert.Equal(1, restored.Chart.Recognition.Confidence);
        Assert.Equal("pxaMetadata", restored.Chart.Recognition.SourceKind);
        Assert.DoesNotContain(elements, element => element.Type == "shape");
        Assert.Contains(elements, element => element.Content == "Outside chart");
        Assert.Contains(imported.ImportDiagnostics!, item => item.Code == "PXA-PDF-CHART-002");
    }
}
