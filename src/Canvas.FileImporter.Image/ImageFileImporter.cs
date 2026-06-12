using Canvas.Core.Contracts;
using Canvas.FileImporter.Abstractions;
using SkiaSharp;

namespace Canvas.FileImporter.Image;

/// <summary>
/// Converts a raster image file (PNG, JPG, JPEG, GIF, WebP, BMP, TIFF) into a
/// single-page <see cref="DesignExportDto"/> that faithfully reproduces the
/// image on a standard-sized page.
///
/// <para>The image is placed on an A4 page (portrait or landscape, matching the
/// image aspect ratio), centered and scaled to fit within a margin while
/// preserving aspect ratio. EXIF orientation is honoured so phone photos are
/// upright, and the original encoded bytes are passed through when possible so
/// JPEGs keep their native quality and small size.</para>
/// </summary>
public sealed class ImageFileImporter : IFileImporter
{
    // A4 in PDF points (1pt = 1/72in).
    private const double A4ShortSide = 595;   // 210mm
    private const double A4LongSide  = 842;   // 297mm
    private const double Margin      = 36;    // 0.5in

    public IReadOnlyList<string> SupportedExtensions { get; } =
        ["png", "jpg", "jpeg", "gif", "webp", "bmp", "tiff", "tif"];

    public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        Task.FromResult(Import(stream, name ?? "image"));

    public static DesignExportDto Import(Stream stream, string originalFileName)
    {
        // SKCodec needs a seekable stream; buffer arbitrary caller streams.
        var raw = ReadAllBytes(stream);

        using var codec = SKCodec.Create(new SKMemoryStream(raw))
            ?? throw new InvalidOperationException("Unable to decode image — unsupported or corrupt file.");

        var origin = codec.EncodedOrigin;

        using var decoded = SKBitmap.Decode(codec)
            ?? throw new InvalidOperationException("Unable to decode image — unsupported or corrupt file.");

        using var bitmap = ApplyOrientation(decoded, origin);

        var (dataUri, _) = EncodeDataUri(raw, codec.EncodedFormat, bitmap, origin);

        double imgW = bitmap.Width;
        double imgH = bitmap.Height;

        // Page orientation follows the (corrected) image aspect ratio.
        bool landscape = imgW > imgH;
        double pageW = landscape ? A4LongSide  : A4ShortSide;
        double pageH = landscape ? A4ShortSide : A4LongSide;

        return new DesignExportDto
        {
            Id   = Guid.NewGuid().ToString(),
            Name = Path.GetFileNameWithoutExtension(originalFileName),
            Pages =
            [
                new PageDto
                {
                    Id       = Guid.NewGuid().ToString(),
                    Elements =
                    [
                        new ElementDto
                        {
                            Id                   = Guid.NewGuid().ToString(),
                            Type                 = "image",
                            X                    = Margin,
                            Y                    = Margin,
                            Width                = pageW - 2 * Margin,
                            Height               = pageH - 2 * Margin,
                            Content              = dataUri,
                            FitMode              = "contain",
                            PreserveAspectRatio  = true,
                        }
                    ]
                }
            ],
            PageSettings = new PageSettingsDto
            {
                Width       = pageW,
                Height      = pageH,
                Orientation = landscape ? "landscape" : "portrait",
                Unit        = "pt",
            },
        };
    }

    /// <summary>
    /// Produces a data URI for the image. When no orientation transform was
    /// applied and the source is JPEG/PNG, the original encoded bytes are passed
    /// through (preserving native quality and small size). Otherwise the
    /// re-oriented bitmap is re-encoded — PNG when it has transparency, else
    /// JPEG at high quality.
    /// </summary>
    private static (string DataUri, string Mime) EncodeDataUri(
        byte[] raw, SKEncodedImageFormat sourceFormat, SKBitmap oriented, SKEncodedOrigin origin)
    {
        bool transformed = origin != SKEncodedOrigin.TopLeft;

        if (!transformed && sourceFormat == SKEncodedImageFormat.Jpeg)
            return ($"data:image/jpeg;base64,{Convert.ToBase64String(raw)}", "image/jpeg");

        if (!transformed && sourceFormat == SKEncodedImageFormat.Png)
            return ($"data:image/png;base64,{Convert.ToBase64String(raw)}", "image/png");

        using var image = SKImage.FromBitmap(oriented);

        if (HasTransparency(oriented))
        {
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return ($"data:image/png;base64,{Convert.ToBase64String(png.ToArray())}", "image/png");
        }

        using var jpg = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return ($"data:image/jpeg;base64,{Convert.ToBase64String(jpg.ToArray())}", "image/jpeg");
    }

    /// <summary>
    /// Returns a bitmap rotated/flipped so that the visual top-left matches the
    /// pixel top-left, given the EXIF-encoded origin. Returns the input unchanged
    /// when the origin is already <see cref="SKEncodedOrigin.TopLeft"/>.
    /// </summary>
    private static SKBitmap ApplyOrientation(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return src.Copy();

        // Origins that rotate by 90/270 degrees swap width and height.
        bool swap = origin is SKEncodedOrigin.LeftTop
                            or SKEncodedOrigin.RightTop
                            or SKEncodedOrigin.RightBottom
                            or SKEncodedOrigin.LeftBottom;

        int dstW = swap ? src.Height : src.Width;
        int dstH = swap ? src.Width  : src.Height;

        var dst = new SKBitmap(dstW, dstH, src.ColorType, src.AlphaType);
        using var canvas = new SKCanvas(dst);

        // Build the transform that maps source pixels into the upright bitmap.
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:      // flip horizontal
                canvas.Translate(dstW, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:   // rotate 180
                canvas.Translate(dstW, dstH);
                canvas.Scale(-1, -1);
                break;
            case SKEncodedOrigin.BottomLeft:    // flip vertical
                canvas.Translate(0, dstH);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:       // transpose
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:      // rotate 90 CW
                canvas.Translate(dstW, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:   // transverse
                canvas.Translate(dstW, dstH);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:    // rotate 270 CW
                canvas.Translate(0, dstH);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(src, 0, 0);
        canvas.Flush();
        return dst;
    }

    private static bool HasTransparency(SKBitmap bitmap) =>
        bitmap.AlphaType is SKAlphaType.Premul or SKAlphaType.Unpremul;

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
