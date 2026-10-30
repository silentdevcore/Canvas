using PVAD.Core.Contracts;
using PVAD.Core.Primitives;

namespace PVAD.Core.Tests;

public sealed class ChartDefinitionNormalizerTests
{
    [Theory]
    [InlineData("pxaMetadata", "pvadMetadata")]
    [InlineData("pvadMetadata", "pvadMetadata")]
    [InlineData("pdfVector", "pdfVector")]
    [InlineData("pdfRaster", "pdfRaster")]
    [InlineData(null, null)]
    public void Recognition_sources_are_canonical_without_mutating_the_input(string? source, string? expected)
    {
        var original = new ChartDefinitionDto
        {
            Categories = ["A"],
            Series = [new ChartSeriesDto { Values = [-2, null, 3] }],
            Recognition = new ChartRecognitionDto { SourceKind = source, Confidence = 0.9 }
        };
        var element = new ElementDto
        {
            Chart = original,
            Style = new Dictionary<string, object> { ["pdfChartRecognitionSource"] = source ?? "pdfVector", ["custom"] = 7 }
        };

        var normalized = ChartDefinitionNormalizer.Normalize(element);
        Assert.Equal(expected, normalized.Recognition!.SourceKind);
        Assert.Equal(source, original.Recognition.SourceKind);
        Assert.Equal([-2d, null, 3d], normalized.Series[0].Values);
        Assert.Equal(0.9, normalized.Recognition.Confidence);

        ChartDefinitionNormalizer.SynchronizeLegacyFields(element);
        Assert.Equal(expected ?? "pdfVector", element.Style["pdfChartRecognitionSource"]);
        Assert.Equal(7, element.Style["custom"]);
        Assert.Equal(expected, ChartDefinitionNormalizer.Normalize(element).Recognition!.SourceKind);
    }

    [Fact]
    public void Legacy_chart_data_is_upgraded_without_losing_series()
    {
        var element = new ElementDto
        {
            Type = "chart",
            ChartType = "line",
            ChartData = new Dictionary<string, object>
            {
                ["labels"] = new[] { "Jan", "Feb" },
                ["datasets"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["label"] = "Revenue",
                        ["data"] = new object?[] { -4, null },
                        ["backgroundColor"] = "#123456"
                    },
                    new Dictionary<string, object>
                    {
                        ["label"] = "Cost",
                        ["data"] = new[] { 2, 3 }
                    }
                }
            }
        };

        var chart = ChartDefinitionNormalizer.Normalize(element);

        Assert.Equal(2, chart.SchemaVersion);
        Assert.Equal(PvadChartTypes.Line, chart.Type);
        Assert.Equal(["Jan", "Feb"], chart.Categories);
        Assert.Equal(2, chart.Series.Count);
        Assert.Equal([-4d, null], chart.Series[0].Values);
        Assert.Equal("#123456", chart.Series[0].Color);
    }

    [Fact]
    public void Version_two_chart_is_bounded_and_normalized()
    {
        var element = new ElementDto
        {
            Type = "chart",
            Chart = new ChartDefinitionDto
            {
                Type = "STACKEDBAR",
                Categories = Enumerable.Range(0, ChartDefinitionNormalizer.MaximumPoints + 10)
                    .Select(index => index.ToString()).ToList(),
                Series =
                [
                    new ChartSeriesDto { Id = "", Name = "", Values = [1, double.NaN, -2] }
                ],
                Recognition = new ChartRecognitionDto { Status = "reviewRequired", Confidence = 5 }
            }
        };

        var chart = ChartDefinitionNormalizer.Normalize(element);

        Assert.Equal(PvadChartTypes.StackedBar, chart.Type);
        Assert.Equal(ChartDefinitionNormalizer.MaximumPoints, chart.Categories.Count);
        Assert.Equal("series-1", chart.Series[0].Id);
        Assert.Equal("default", chart.Series[0].StackGroup);
        Assert.Null(chart.Series[0].Values[1]);
        Assert.Equal(1, chart.Recognition!.Confidence);
    }

    [Fact]
    public void Synchronize_legacy_fields_preserves_provider_metadata()
    {
        var element = new ElementDto
        {
            Type = "chart",
            Chart = new ChartDefinitionDto
            {
                Type = PvadChartTypes.Area,
                Categories = ["A"],
                Series = [new ChartSeriesDto { Id = "sales", Name = "Sales", Values = [10] }]
            },
            ChartData = new Dictionary<string, object> { ["rdlDataSetName"] = "SalesData" }
        };

        ChartDefinitionNormalizer.SynchronizeLegacyFields(element);

        Assert.Equal(PvadChartTypes.Area, element.ChartType);
        Assert.Equal("SalesData", element.ChartData!["rdlDataSetName"]);
        Assert.Single((object[])element.ChartData["datasets"]);
    }
}
