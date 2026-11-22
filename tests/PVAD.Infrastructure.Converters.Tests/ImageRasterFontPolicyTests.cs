using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Converters;
using SkiaSharp;

namespace PVAD.Infrastructure.Converters.Tests;

public sealed class ImageRasterFontPolicyTests
{
    [Theory]
    [InlineData("Arial", 14f)]
    [InlineData("Arial", 29.166666f)]
    [InlineData("Times New Roman", 9f)]
    [InlineData("Courier New", 10f)]
    public void RasterFont_DisablesOnlyWindowsHintingWithoutChangingOtherSettings(string family, float size)
    {
        using var typeface = SKTypeface.FromFamilyName(family);
        using var defaults = new SKFont(typeface, size);
        var factory = typeof(ImageDocumentExporter).GetMethod("CreateRasterFont", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var actual = (SKFont)factory.Invoke(null, [typeface, size])!;

        Assert.Equal(OperatingSystem.IsWindows() ? SKFontHinting.None : defaults.Hinting, actual.Hinting);
        Assert.Equal(defaults.Size, actual.Size);
        Assert.Equal(defaults.Edging, actual.Edging);
        Assert.Equal(defaults.Subpixel, actual.Subpixel);
        Assert.Equal(defaults.LinearMetrics, actual.LinearMetrics);
        Assert.Equal(defaults.EmbeddedBitmaps, actual.EmbeddedBitmaps);
        Assert.Equal(defaults.BaselineSnap, actual.BaselineSnap);
        foreach (var text in new[] { "Synthetic", "converter", "content" })
        {
            Assert.Equal(defaults.MeasureText(text), actual.MeasureText(text));
            using var expectedPath = defaults.GetTextPath(text, new SKPoint(5, 19));
            using var actualPath = actual.GetTextPath(text, new SKPoint(5, 19));
            Assert.Equal(expectedPath.ToSvgPathData(), actualPath.ToSvgPathData());
        }
    }

    [Theory]
    [InlineData("png", 72)]
    [InlineData("png", 150)]
    [InlineData("jpeg", 72)]
    [InlineData("jpeg", 150)]
    [InlineData("tiff", 72)]
    [InlineData("tiff", 150)]
    public void RasterExports_RepeatExactlyWithMixedPagesAndDoNotMutateDesign(string format, int dpi)
    {
        DocumentExporter exporter = format switch
        {
            "png" => new ImageDocumentExporter(),
            "jpeg" => new JpegDocumentExporter(),
            _ => new TiffDocumentExporter(),
        };
        var design = new DesignExportDto
        {
            PageSettings = new() { Width = 120, Height = 160 },
            Pages = Enumerable.Range(0, 2).Select(index => new PageDto
            {
                Id = "page-" + index,
                Layout = index == 0 ? null : new() { Width = 160, Height = 120, Orientation = "landscape" },
                Elements = [new() { Id = "text-" + index, Type = "text", Content = "Synthetic converter content", X = 5, Y = 5, Width = 100, Height = 40 }],
            }).ToList(),
        };
        var before = JsonSerializer.Serialize(design);
        var expected = Parts(exporter.Export(design, new(Dpi: dpi)));
        Assert.Equal(2, expected.Length);
        for (var repeat = 0; repeat < 4; repeat++)
        {
            var actual = Parts(exporter.Export(design, new(Dpi: dpi)));
            Assert.Equal(expected.Length, actual.Length);
            for (var page = 0; page < actual.Length; page++) Assert.Equal(expected[page], actual[page]);
        }
        Assert.Equal(before, JsonSerializer.Serialize(design));
    }

    private static byte[][] Parts(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        return zip.Entries.Select(entry =>
        {
            using var source = entry.Open();
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            return buffer.ToArray();
        }).ToArray();
    }
}
