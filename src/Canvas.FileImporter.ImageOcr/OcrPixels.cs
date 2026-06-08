using SkiaSharp;

namespace Canvas.FileImporter.ImageOcr;

// Fast, read-only pixel accessor for the layout-detection stages. SKBitmap.GetPixel
// marshals managed<->native on every call, which is catastrophically slow when the
// detectors make many full-image passes. OcrPixels snapshots the whole bitmap into a
// managed array once (one bulk interop call) and serves pixels from memory, giving the
// exact same unpremultiplied colors GetPixel would return.
internal sealed class OcrPixels
{
    private readonly SKColor[] _pixels;

    public OcrPixels(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Width = bitmap.Width;
        Height = bitmap.Height;
        _pixels = bitmap.Pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public SKColor GetPixel(int x, int y) => _pixels[y * Width + x];

    public static implicit operator OcrPixels(SKBitmap bitmap) => new(bitmap);
}
