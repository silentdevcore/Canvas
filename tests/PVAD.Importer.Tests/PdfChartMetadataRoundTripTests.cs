using PVAD.Core.Contracts;
using PVAD.Core.Primitives;
using PVAD.FileImporter;
using PVAD.Pdf;

namespace PVAD.Importer.Tests;

public sealed class PdfChartMetadataRoundTripTests
{
    [Theory]
    [InlineData("PVADChartsV2", false)]
    [InlineData("PXAChartsV2", false)]
    [InlineData("PVADChartsV2", true)]
    public async Task Import_RestoresPvadChartMetadata_AndConsumesRenderedPrimitives(string key, bool includeAlias)
    {
        var chart = new ElementDto
        {
            Id = "sales-chart", Type = "chart", X = 40, Y = 60, Width = 300, Height = 180,
            Chart = new ChartDefinitionDto
            {
                Type = PvadChartTypes.Bar,
                Categories = ["A", "B", "C"],
                Series = [new ChartSeriesDto { Id = "sales", Name = "Sales", Values = [4, 8, 6] }]
            }
        };
        var encoded = ChartMetadataCodec.Encode([new PlannedPage("page-1", [chart])]);

#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        document.Info.CustomProperties[key] = Assert.IsType<string>(encoded);
        if (includeAlias) document.Info.CustomProperties[ChartMetadataCodec.LegacyPdfInfoKey] = encoded;
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
        Assert.Equal("pvadMetadata", restored.Chart.Recognition.SourceKind);
        Assert.Equal("pvadMetadata", restored.Style!["pdfChartRecognitionSource"]);
        Assert.DoesNotContain(elements, element => element.Type == "shape");
        Assert.Contains(elements, element => element.Content == "Outside chart");
        Assert.Contains(imported.ImportDiagnostics!, item => item.Code == "PXA-PDF-CHART-002");
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("invalid-canonical")]
    [InlineData("invalid-legacy")]
    public async Task Import_preserves_graphics_when_metadata_is_invalid_or_conflicting(string scenario)
    {
        string Payload(double value) => Assert.IsType<string>(ChartMetadataCodec.Encode([
            new PlannedPage("page-1", [new ElementDto
            {
                Id = "chart-1", Type = "chart", X = 40, Y = 60, Width = 300, Height = 180,
                Chart = new ChartDefinitionDto
                {
                    Categories = ["A"], Series = [new ChartSeriesDto { Values = [value] }],
                },
            }]),
        ]));

#pragma warning disable PXA0001
        var document = new PdfDocument();
#pragma warning restore PXA0001
        if (scenario != "invalid-legacy")
            document.Info.CustomProperties[ChartMetadataCodec.PdfInfoKey] =
                scenario == "conflict" ? Payload(10) : "not-base64";
        document.Info.CustomProperties[ChartMetadataCodec.LegacyPdfInfoKey] =
            scenario == "invalid-legacy" ? "not-base64" : Payload(5);
        var page = document.AddPage(595, 842);
        page.DrawRectangle(40, 602, 300, 180, fill: true, fillColor: PdfColor.FromRgb(37, 99, 235));

        using var stream = new MemoryStream(document.ToBytes());
        var imported = await PdfFileImporter.DoImportAsync(stream, "Metadata review");

        var elements = Assert.Single(imported.Pages).Elements;
        Assert.DoesNotContain(elements, element => element.Type == "chart");
        Assert.Contains(elements, element => element.Type == "shape");
        Assert.Contains(imported.ImportDiagnostics!, item => item.Code == "PXA-PDF-CHART-001");
    }
}
