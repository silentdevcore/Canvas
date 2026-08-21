using PXA.Core.Contracts;
using PXA.Core.Primitives;

namespace PXA.Core.Tests;

public sealed class ChartMetadataCodecTests
{
    [Fact]
    public void EncodeAndDecode_PreservesNormalizedChartAndBounds()
    {
        var pages = new[]
        {
            new PlannedPage("page-1",
            [
                new ElementDto
                {
                    Id = "chart-1", Type = "chart", X = 10, Y = 20, Width = 300, Height = 180,
                    Chart = new ChartDefinitionDto
                    {
                        Type = PxaChartTypes.Line, Categories = ["A", "B"],
                        Series = [new ChartSeriesDto { Id = "s1", Name = "Sales", Values = [1, 2] }]
                    }
                }
            ])
        };

        var encoded = ChartMetadataCodec.Encode(pages);

        Assert.True(ChartMetadataCodec.TryDecode(encoded, out var decoded));
        var chart = Assert.Single(decoded.Charts);
        Assert.Equal("chart-1", chart.ElementId);
        Assert.Equal(300, chart.Width);
        Assert.Equal(PxaChartTypes.Line, chart.Definition.Type);
        Assert.Equal(ChartMetadataCodec.ComputeHash(chart.Definition), chart.Hash);
    }

    [Fact]
    public void TryDecode_RejectsMalformedOrTamperedPayloads()
    {
        Assert.False(ChartMetadataCodec.TryDecode("not-base64", out _));
        Assert.False(ChartMetadataCodec.TryDecode(new string('A', ChartMetadataCodec.MaximumCompressedBytes * 2 + 1), out _));
    }
}
