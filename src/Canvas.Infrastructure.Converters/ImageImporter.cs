using Canvas.Core.Contracts;
using SkiaSharp;

namespace Canvas.Infrastructure.Converters;

/// <summary>
/// Converts a raster image file (PNG, JPG, JPEG, GIF, WebP, BMP, TIFF, …)
/// into a <see cref="DesignExportDto"/> with a single page whose dimensions
/// match the image's native pixel size and one full-page Image element.
/// </summary>
public static class ImageImporter
{
    public static DesignExportDto Import(Stream stream, string originalFileName)
    {
        using var bitmap = SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException("Unable to decode image — unsupported or corrupt file.");

        double w = bitmap.Width;
        double h = bitmap.Height;

        using var image   = SKImage.FromBitmap(bitmap);
        using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        var b64     = Convert.ToBase64String(pngData.ToArray());
        var dataUri = $"data:image/png;base64,{b64}";

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
                            Id      = Guid.NewGuid().ToString(),
                            Type    = "image",
                            X       = 0,
                            Y       = 0,
                            Width   = w,
                            Height  = h,
                            Content = dataUri,
                        }
                    ]
                }
            ],
            PageSettings = new PageSettingsDto
            {
                Width  = w,
                Height = h,
            },
        };
    }
}
