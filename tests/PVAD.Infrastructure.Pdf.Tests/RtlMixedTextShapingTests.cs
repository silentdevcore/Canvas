using System.Text.RegularExpressions;
using System.Runtime;
using PVAD.Pdf;

namespace PVAD.Infrastructure.Pdf.Tests;

public sealed class RtlMixedTextShapingTests
{
    [Theory]
    [InlineData("الجمعة، 13 نوفمبر 2026", "2026", "13")]
    [InlineData("الجمعة، ١٣ نوفمبر ٢٠٢٦", "٢٠٢٦", "١٣")]
    public void ArabicDateShapingPreservesDigitOrder(string text, string first, string second)
    {
        var visual = string.Concat(Font().Shape(text).Select(glyph => glyph.Unicode));
        Assert.Equal(new[] { first, second }, Regex.Matches(visual, @"\d+").Select(match => match.Value));
    }

    [Theory]
    [InlineData("موعد \u20662026-11-13\u2069", "2026-11-13")]
    [InlineData("المبلغ 123.45 CHF", "123.45")]
    [InlineData("مرحبا 😀 2026", "2026")]
    public void MixedRunsRetainNumbersAndClusterIntegrity(string text, string expected)
    {
        var font = Font();
        var glyphs = font.Shape(text);
        var visual = string.Concat(glyphs.Select(glyph => glyph.Unicode));
        Assert.Contains(expected, visual);
        Assert.Equal(glyphs.Sum(glyph => glyph.Advance) / font.UnitsPerEm * 12, font.MeasureWidth(text, 12), 6);
        foreach (var glyph in glyphs)
            Assert.DoesNotContain(glyph.Unicode, value => char.IsSurrogate(value) && glyph.Unicode.Length == 1);
    }

    [Fact]
    public void MixedArabicCurrencyUsesPackagedFontMetrics()
    {
        Assert.Equal(92.376, Font().MeasureWidth("المبلغ 123.45 CHF", 12), 6);
    }

    [Fact]
    public void ShapingBlobRetainsExactFontBytesAcrossCompactingCollections()
    {
        var font = Font();
        using var blob = font.CreateShapingBlob();
        Assert.True(blob.AsSpan().SequenceEqual(font.FontBytes.Span));
        for (var iteration = 0; iteration < 4; iteration++)
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            var pressure = Enumerable.Range(0, 8).Select(_ => new byte[font.FontBytes.Length]).ToArray();
            foreach (var bytes in pressure) Array.Fill(bytes, (byte)0x5a);
            Assert.True(blob.AsSpan().SequenceEqual(font.FontBytes.Span), "Native font bytes changed after a compacting collection.");
            GC.KeepAlive(pressure);
        }
    }

    [Theory]
    [InlineData("المبلغ 123.45 CHF")]
    [InlineData("موعد \u20662026-11-13\u2069")]
    [InlineData("الجمعة، ١٣ نوفمبر ٢٠٢٦")]
    [InlineData("مرحبا 😀 2026")]
    public void ConcurrentShapingKeepsGlyphsAndWidthsStableUnderCollection(string text)
    {
        var font = Font();
        var expected = font.Shape(text).ToArray();
        var expectedWidth = expected.Sum(glyph => glyph.Advance) / font.UnitsPerEm * 12;
        Parallel.For(0, 32, iteration =>
        {
            if (iteration % 8 == 0)
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            Assert.Equal(expected, font.Shape(text));
            Assert.Equal(expectedWidth, font.MeasureWidth(text, 12), 6);
        });
    }

    private static PdfEmbeddedFont Font()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path is not null && !File.Exists(Path.Combine(path.FullName, "PVAD.sln"))) path = path.Parent;
        Assert.NotNull(path);
        Assert.True(PdfEmbeddedFont.TryLoad(Path.Combine(path.FullName, "assets", "fonts", "noto-arabic", "NotoSansArabic-Regular.ttf"), true, out var font));
        return font!;
    }
}
