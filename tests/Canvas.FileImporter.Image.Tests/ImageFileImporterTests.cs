using Canvas.Core.Contracts;
using SkiaSharp;

namespace Canvas.FileImporter.Image.Tests;

public class ImageFileImporterTests
{
    // A4 in points.
    private const double A4Short = 595;
    private const double A4Long  = 842;
    private const double Margin  = 36;

    [Fact]
    public void Import_WideImage_ProducesLandscapeA4Page()
    {
        var design = Import(MakeImage(200, 100, SKEncodedImageFormat.Png));

        var page = design.PageSettings!;
        Assert.Equal(A4Long,  page.Width,  3);
        Assert.Equal(A4Short, page.Height, 3);
        Assert.Equal("landscape", page.Orientation);
        Assert.Equal("pt", page.Unit);
    }

    [Fact]
    public void Import_TallImage_ProducesPortraitA4Page()
    {
        var design = Import(MakeImage(100, 200, SKEncodedImageFormat.Png));

        var page = design.PageSettings!;
        Assert.Equal(A4Short, page.Width,  3);
        Assert.Equal(A4Long,  page.Height, 3);
        Assert.Equal("portrait", page.Orientation);
    }

    [Fact]
    public void Import_SquareImage_DefaultsToPortrait()
    {
        var design = Import(MakeImage(120, 120, SKEncodedImageFormat.Png));
        Assert.Equal("portrait", design.PageSettings!.Orientation);
    }

    [Fact]
    public void Import_PlacesSingleContainedImageElementInsideMargins()
    {
        var design = Import(MakeImage(100, 200, SKEncodedImageFormat.Png));

        var page = Assert.Single(design.Pages);
        var el = Assert.Single(page.Elements);

        Assert.Equal("image", el.Type);
        Assert.Equal("contain", el.FitMode);
        Assert.True(el.PreserveAspectRatio);
        Assert.Equal(Margin, el.X, 3);
        Assert.Equal(Margin, el.Y, 3);
        Assert.Equal(design.PageSettings!.Width  - 2 * Margin, el.Width,  3);
        Assert.Equal(design.PageSettings!.Height - 2 * Margin, el.Height, 3);
    }

    [Fact]
    public void Import_JpegSource_IsPassedThroughAsJpegDataUri()
    {
        var design = Import(MakeImage(200, 100, SKEncodedImageFormat.Jpeg));
        var el = design.Pages[0].Elements[0];
        Assert.StartsWith("data:image/jpeg;base64,", el.Content);
    }

    [Fact]
    public void Import_PngSource_IsPassedThroughAsPngDataUri()
    {
        var design = Import(MakeImage(200, 100, SKEncodedImageFormat.Png));
        var el = design.Pages[0].Elements[0];
        Assert.StartsWith("data:image/png;base64,", el.Content);
    }

    [Fact]
    public void Import_HonoursExifOrientation_RotatesDimensions()
    {
        // 40x20 landscape JPEG tagged Orientation=6 (rotate 90 CW) → upright is
        // 20x40 (portrait), so the page must come out portrait.
        var jpeg = MakeImage(40, 20, SKEncodedImageFormat.Jpeg);
        var tagged = InjectExifOrientation(jpeg, orientation: 6);

        var design = Import(tagged);

        Assert.Equal("portrait", design.PageSettings!.Orientation);
        Assert.Equal(A4Short, design.PageSettings!.Width, 3);
    }

    [Fact]
    public void Import_CorruptData_Throws()
    {
        Assert.ThrowsAny<Exception>(() => Import([0x00, 0x01, 0x02, 0x03]));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DesignExportDto Import(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return ImageFileImporter.Import(ms, "sample.img");
    }

    private static byte[] MakeImage(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.CornflowerBlue };
            canvas.DrawRect(0, 0, width / 2f, height, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>
    /// Inserts a minimal EXIF APP1 segment carrying the given Orientation value
    /// immediately after the JPEG SOI marker, so SkiaSharp reports the encoded
    /// origin without needing a binary fixture file.
    /// </summary>
    private static byte[] InjectExifOrientation(byte[] jpeg, ushort orientation)
    {
        // TIFF (little-endian) with a single Orientation (0x0112) SHORT entry.
        byte[] tiff =
        [
            0x49, 0x49,                                     // "II" little-endian
            0x2A, 0x00,                                     // magic 42
            0x08, 0x00, 0x00, 0x00,                         // offset to IFD0 = 8
            0x01, 0x00,                                     // 1 directory entry
            0x12, 0x01,                                     // tag 0x0112 (Orientation)
            0x03, 0x00,                                     // type SHORT
            0x01, 0x00, 0x00, 0x00,                         // count 1
            (byte)(orientation & 0xFF), (byte)(orientation >> 8), 0x00, 0x00, // value
            0x00, 0x00, 0x00, 0x00,                         // next IFD = 0
        ];

        byte[] exifHeader = "Exif\0\0"u8.ToArray();
        int payloadLen = exifHeader.Length + tiff.Length + 2; // +2 for length field
        byte[] app1 =
        [
            0xFF, 0xE1,                                     // APP1 marker
            (byte)(payloadLen >> 8), (byte)(payloadLen & 0xFF), // big-endian length
            .. exifHeader,
            .. tiff,
        ];

        // jpeg starts with SOI (FFD8); splice APP1 right after it.
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }
}
