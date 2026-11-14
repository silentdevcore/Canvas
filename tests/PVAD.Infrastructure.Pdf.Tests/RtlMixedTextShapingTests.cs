using System.Text.RegularExpressions;
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

    private static PdfEmbeddedFont Font()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path is not null && !File.Exists(Path.Combine(path.FullName, "PVAD.sln"))) path = path.Parent;
        Assert.NotNull(path);
        Assert.True(PdfEmbeddedFont.TryLoad(Path.Combine(path.FullName, "assets", "fonts", "noto-arabic", "NotoSansArabic-Regular.ttf"), true, out var font));
        return font!;
    }
}
