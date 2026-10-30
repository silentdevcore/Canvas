using PVAD.Core.Contracts;
using PVAD.Core.Primitives;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace PVAD.Core.Tests;

public sealed class ChartMetadataCodecTests
{
    [Theory]
    [InlineData("pxaMetadata")]
    [InlineData("pvadMetadata")]
    public void Source_name_transition_validates_original_hashes_before_normalizing(string sourceKind)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var definition = ChartDefinitionNormalizer.Normalize(new ElementDto
        {
            Chart = new ChartDefinitionDto
            {
                Categories = ["A"], Series = [new ChartSeriesDto { Values = [-5] }],
                Recognition = new ChartRecognitionDto { SourceKind = "pvadMetadata" }
            }
        });
        definition.Recognition!.SourceKind = sourceKind;
        var originalHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(definition, options)));
        var entry = new ChartMetadataEntry { ElementId = "chart", Definition = definition, Hash = originalHash };
        var envelope = new ChartMetadataEnvelope { Charts = [entry] };
        string Encode()
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
                gzip.Write(JsonSerializer.SerializeToUtf8Bytes(envelope, options));
            return Convert.ToBase64String(output.ToArray());
        }

        var encoded = Encode();
        Assert.True(ChartMetadataCodec.TryDecode(encoded, out var result));
        var decoded = Assert.Single(result.Charts);
        Assert.Equal("pvadMetadata", decoded.Definition.Recognition!.SourceKind);
        Assert.Equal(originalHash, decoded.Hash);
        Assert.Equal(sourceKind, definition.Recognition.SourceKind);
        Assert.Equal(encoded, Encode());

        definition.Series[0].Values[0] = 99;
        Assert.False(ChartMetadataCodec.TryDecode(Encode(), out _));
    }

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
                        Type = PvadChartTypes.Line, Categories = ["A", "B"],
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
        Assert.Equal(PvadChartTypes.Line, chart.Definition.Type);
        Assert.Equal(ChartMetadataCodec.ComputeHash(chart.Definition), chart.Hash);
    }

    [Fact]
    public void TryDecode_RejectsMalformedOrTamperedPayloads()
    {
        Assert.False(ChartMetadataCodec.TryDecode("not-base64", out _));
        Assert.False(ChartMetadataCodec.TryDecode(new string('A', ChartMetadataCodec.MaximumCompressedBytes * 2 + 1), out _));
    }
}
