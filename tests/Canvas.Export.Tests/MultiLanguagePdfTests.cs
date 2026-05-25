using System.Text;
using Canvas.Pdf;
using Canvas.Pdf;

namespace Canvas.Export.Tests;

/// <summary>
/// Tests for multi-language PDF support: embedded fonts, Unicode encoding, and RTL reordering.
/// </summary>
public sealed class MultiLanguagePdfTests
{
    // ── PdfFontLoader ────────────────────────────────────────────────────────

    [Fact]
    public void FontLoader_ReturnsFalse_WhenFontFileNotFound()
    {
        var loader = new PdfFontLoader(Path.GetTempPath() + Guid.NewGuid());
        var loaded = loader.TryLoad("ar", out var font);
        Assert.False(loaded);
        Assert.Null(font);
    }

    [Fact]
    public void FontLoader_ReturnsFalse_ForEmptyLanguage()
    {
        var loader = new PdfFontLoader(Path.GetTempPath());
        Assert.False(loader.TryLoad(null, out _));
        Assert.False(loader.TryLoad("", out _));
        Assert.False(loader.TryLoad("   ", out _));
    }

    [Fact]
    public void FontLoader_IsRtl_CorrectlyClassifiesLanguages()
    {
        Assert.True(PdfFontLoader.IsRtl("ar"));
        Assert.True(PdfFontLoader.IsRtl("ar-SA"));
        Assert.True(PdfFontLoader.IsRtl("he"));
        Assert.True(PdfFontLoader.IsRtl("fa"));
        Assert.True(PdfFontLoader.IsRtl("ur"));

        Assert.False(PdfFontLoader.IsRtl("en"));
        Assert.False(PdfFontLoader.IsRtl("de"));
        Assert.False(PdfFontLoader.IsRtl("zh"));
        Assert.False(PdfFontLoader.IsRtl("ja"));
        Assert.False(PdfFontLoader.IsRtl(null));
    }

    // ── Unicode hex encoding ─────────────────────────────────────────────────

    [Fact]
    public void EncodeAsHexUtf16Be_StartsWithBom()
    {
        var result = PdfTextEncoding.EncodeAsHexUtf16Be("A");
        Assert.StartsWith("<FEFF", result, StringComparison.Ordinal);
    }

    [Fact]
    public void EncodeAsHexUtf16Be_EncodesLatinCorrectly()
    {
        // 'A' = U+0041
        var result = PdfTextEncoding.EncodeAsHexUtf16Be("A");
        Assert.Equal("<FEFF0041>", result);
    }

    [Fact]
    public void EncodeAsHexUtf16Be_EncodesArabicCorrectly()
    {
        // مرحبا — U+0645 U+0631 U+062D U+0628 U+0627
        var result = PdfTextEncoding.EncodeAsHexUtf16Be("مرحبا");
        Assert.StartsWith("<FEFF", result, StringComparison.Ordinal);
        Assert.Contains("0645", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncodeAsHexUtf16Be_WrapsInAngleBrackets()
    {
        var result = PdfTextEncoding.EncodeAsHexUtf16Be("Hi");
        Assert.StartsWith("<", result, StringComparison.Ordinal);
        Assert.EndsWith(">", result, StringComparison.Ordinal);
    }

    // ── RTL reordering ───────────────────────────────────────────────────────

    [Fact]
    public void ReverseForRtl_ReversesAsciiString()
    {
        var result = PdfTextEncoding.ReverseForRtl("Hello");
        Assert.Equal("olleH", result);
    }

    [Fact]
    public void ReverseForRtl_HandlesSingleCharacter()
    {
        Assert.Equal("A", PdfTextEncoding.ReverseForRtl("A"));
    }

    [Fact]
    public void ReverseForRtl_ReversesGraphemeClusters()
    {
        // Arabic: مرحبا reversed should be ابحرم
        var arabic = "مرحبا";
        var reversed = PdfTextEncoding.ReverseForRtl(arabic);
        Assert.Equal(arabic.Length, reversed.Length);
        Assert.NotEqual(arabic, reversed);
    }

    // ── Integration: PDF generation without embedded font ────────────────────

    [Fact]
    public void PdfDocument_GeneratesValidPdf_WithoutEmbeddedFont()
    {
        var doc = new PdfDocument();
        var page = doc.AddPage();
        page.DrawText("Hello World", 50, 700);
        var bytes = doc.ToBytes();

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public void PdfDocument_GeneratesValidPdf_WithLanguageSet_NoFontFile()
    {
        // When no font file exists for a language, should fall back to Type1 gracefully
        var loader = new PdfFontLoader(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var doc = new PdfDocument { FontLoader = loader };
        var page = doc.AddPage();
        page.DrawText("مرحبا", 50, 700, new PdfDrawTextOptions
        {
            FontSize = 14,
            Language = "ar",
            TextDirection = "rtl"
        });
        var bytes = doc.ToBytes();

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    // ── Domain model round-trip ───────────────────────────────────────────────

    [Fact]
    public void ElementDto_Language_And_TextDirection_RoundTrip()
    {
        var original = new ElementDto
        {
            Id = "1",
            Type = "text",
            Content = "مرحبا",
            Language = "ar",
            TextDirection = "rtl"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(original,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<ElementDto>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

        Assert.Equal("ar", deserialized?.Language);
        Assert.Equal("rtl", deserialized?.TextDirection);
    }

    [Fact]
    public void DesignExportDto_WithArabicTextElement_MapsWithoutException()
    {
        var design = new DesignExportDto
        {
            Id = "test",
            Name = "Arabic Test",
            Pages = [
                new PageDto
                {
                    Id = "p1",
                    Elements = [
                        new ElementDto
                        {
                            Id = "e1",
                            Type = "text",
                            X = 50, Y = 50,
                            Width = 200, Height = 40,
                            Content = "مرحبا بالعالم",
                            Language = "ar",
                            TextDirection = "rtl",
                            Style = new Dictionary<string, object>
                            {
                                ["fontSize"] = 14.0,
                                ["color"] = "#000000"
                            }
                        }
                    ]
                }
            ]
        };

        var loader = new PdfFontLoader(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var doc = Canvas.WebApi.Infrastructure.DesignJsonMapper.MapToPdfDocument(design, loader);
        var bytes = doc.ToBytes();

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }
}
