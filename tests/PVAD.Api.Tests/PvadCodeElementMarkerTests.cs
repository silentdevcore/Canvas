using PVAD.Core.Contracts;

namespace PVAD.Api.Tests;

public sealed class PvadCodeElementMarkerTests
{
    [Theory]
    [InlineData("// pvad-element-id: text-1", "text-1")]
    [InlineData("// pxa-element-id: legacy-1", "legacy-1")]
    [InlineData("  // PVAD-ELEMENT-ID: text-2  ", "text-2")]
    [InlineData("// pvad-element-id:", null)]
    [InlineData("// unrelated", null)]
    public void ReadsCanonicalAndLegacyMarkers(string line, string? expected) =>
        Assert.Equal(expected, PvadCodeElementMarker.Read(line));

    [Fact]
    public void RejectsOversizedIds() =>
        Assert.Null(PvadCodeElementMarker.Read("// pvad-element-id: " + new string('x', 201)));
}
