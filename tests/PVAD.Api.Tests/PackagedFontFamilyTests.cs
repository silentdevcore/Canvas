using System.Security.Cryptography;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.Importer;
using PVAD.Pdf;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PackagedFontFamilyTests
{
    [Theory]
    [InlineData(PdfFontFamily.Times, false, false, "Regular", "LiberationSerif")]
    [InlineData(PdfFontFamily.Times, true, false, "Bold", "LiberationSerifBold")]
    [InlineData(PdfFontFamily.Times, false, true, "Italic", "LiberationSerifItalic")]
    [InlineData(PdfFontFamily.Times, true, true, "BoldItalic", "LiberationSerifBoldItalic")]
    [InlineData(PdfFontFamily.Courier, false, false, "Regular", "LiberationMono")]
    [InlineData(PdfFontFamily.Courier, true, false, "Bold", "LiberationMonoBold")]
    [InlineData(PdfFontFamily.Courier, false, true, "Italic", "LiberationMonoItalic")]
    [InlineData(PdfFontFamily.Courier, true, true, "BoldItalic", "LiberationMonoBoldItalic")]
    public async Task EachFaceIsLoadedAndEmbeddedWithoutSubstitution(PdfFontFamily family,
        bool bold, bool italic, string style, string expectedName)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "fonts");
        var loader = new PdfFontLoader(directory);
        Assert.True(loader.TryLoad(null, bold, italic, family, out var font));
        Assert.Equal(expectedName, font!.BaseFontName);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "serif-mono-provenance.json")));
        var face = Assert.Single(manifest.RootElement.GetProperty("faces").EnumerateArray(),
            item => item.GetProperty("family").GetString() == (family == PdfFontFamily.Times ? "Serif" : "Mono")
                && item.GetProperty("style").GetString() == style);
        Assert.Equal(face.GetProperty("sha256").GetString(),
            Convert.ToHexString(SHA256.HashData(font.FontBytes.Span)).ToLowerInvariant());
        var design = new DesignExportDto { Pages = [new() { Id = "page", Elements = [new()
        {
            Id = "text", Type = "text", Content = "Office 0123456789 Zürich", X = 20, Y = 20, Width = 400, Height = 60,
            Style = new() { ["fontFamily"] = family == PdfFontFamily.Times ? "Georgia" : "Consolas",
                ["fontWeight"] = bold ? "bold" : "normal", ["fontStyle"] = italic ? "italic" : "normal", ["fontSize"] = 14 }
        }] }] };
        foreach (var type in new[] { "text", "richtext", "link" })
        {
            var element = design.Pages[0].Elements[0];
            element.Type = type;
            element.HtmlContent = (bold ? "<strong>" : "") + (italic ? "<em>" : "") + element.Content
                + (italic ? "</em>" : "") + (bold ? "</strong>" : "");
            var source = JsonSerializer.Serialize(design);
            var document = DesignJsonMapper.MapToPdfDocument(design, loader);
            var imported = await new PdfImporter().LoadAsync(new MemoryStream(document.ToBytes()));
            Assert.NotEmpty(imported.Pages[0].TextObjects);
            Assert.All(imported.Pages[0].TextObjects, text =>
            {
                Assert.Equal(expectedName, text.FontName);
                Assert.Equal(font.FontBytes.ToArray(), text.EmbeddedFontBytes.ToArray());
                Assert.Equal(bold, text.Bold);
                Assert.Equal(italic, text.Italic);
            });
            Assert.Equal(source, JsonSerializer.Serialize(design));
        }
    }

    [Fact]
    public async Task MonospaceRichTextRunsDoNotOverlapAfterAStyleChange()
    {
        var design = new DesignExportDto { Pages = [new() { Id = "page", Elements = [new()
        {
            Id = "rich", Type = "richtext", HtmlContent = "mono regular <strong>bold</strong> <em>italic</em>",
            X = 20, Y = 20, Width = 400, Height = 60, Style = new() { ["fontFamily"] = "Courier New", ["fontSize"] = 14 }
        }] }] };
        var loader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));
        var imported = await new PdfImporter().LoadAsync(new MemoryStream(DesignJsonMapper.MapToPdfDocument(design, loader).ToBytes()));
        var runs = imported.Pages[0].TextObjects.ToArray();
        Assert.True(runs.Length >= 4);
        for (var index = 1; index < runs.Length; index++)
            Assert.True(runs[index].Transform.E >= runs[index - 1].Transform.E
                + runs[index - 1].Glyphs.Sum(glyph => glyph.Advance) - 0.01);
    }

    [Fact]
    public void ExistingRichTextEntryPointRetainsThePagesDefaultFont()
    {
        var signature = new[] { typeof(PdfPage), typeof(string), typeof(double), typeof(double),
            typeof(double), typeof(double), typeof(PdfColor), typeof(double?) };
        Assert.NotNull(typeof(PdfRichTextRenderer).GetMethod("Render", signature));
        var document = new PdfDocument { DefaultFont = PdfStandardFont.TimesRoman };
        PdfRichTextRenderer.Render(document.AddPage(300, 200), "Legacy", 20, 150, 200, 12, PdfColor.Black);
        Assert.Contains("/BaseFont /Times-Roman", System.Text.Encoding.Latin1.GetString(document.ToBytes()));
    }

    [Fact]
    public void MissingSerifAndMonoDoNotFallBackToAnUnrelatedSansFace()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pvad-font-family-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "fonts", "LiberationSans-Regular.ttf"),
                Path.Combine(directory, "LiberationSans-Regular.ttf"));
            var loader = new PdfFontLoader(directory);
            Assert.True(loader.TryLoad(null, out _));
            Assert.False(loader.TryLoad(null, false, false, PdfFontFamily.Times, out _));
            Assert.False(loader.TryLoad(null, false, false, PdfFontFamily.Courier, out _));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
