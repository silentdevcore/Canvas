using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;

if (args.Length != 0) return 64;
var rows = new List<object>();
foreach (var variant in new[] { "default", "grayscale", "unhinted", "linearMetrics", "noEmbeddedBitmaps", "outline" })
foreach (var dpi in new[] { 72f, 150f })
foreach (var landscape in new[] { false, true })
foreach (var repeat in Enumerable.Range(0, 4))
{
    var scale = dpi / 72f;
    var width = (int)((landscape ? 160 : 120) * scale);
    var height = (int)((landscape ? 120 : 160) * scale);
    using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.White);
    using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
    using var font = new SKFont(typeface, 14f * scale);
    switch (variant)
    {
        case "grayscale": font.Edging = SKFontEdging.Antialias; break;
        case "unhinted": font.Hinting = SKFontHinting.None; break;
        case "linearMetrics": font.LinearMetrics = true; break;
        case "noEmbeddedBitmaps": font.EmbeddedBitmaps = false; break;
    }
    using var paint = new SKPaint { Color = SKColor.Parse("#111827"), IsAntialias = true };
    var lines = new List<string>();
    var current = "";
    foreach (var word in "Synthetic converter content".Split(' '))
    {
        var candidate = current.Length == 0 ? word : current + " " + word;
        if (current.Length == 0 || font.MeasureText(candidate) <= 100 * scale) current = candidate;
        else { lines.Add(current); current = word; }
    }
    lines.Add(current);
    var baseline = 5 * scale + font.Size;
    var paths = new List<string>();
    foreach (var line in lines)
    {
        using var path = font.GetTextPath(line, new SKPoint(5 * scale, baseline));
        paths.Add(path.ToSvgPathData());
        if (variant == "outline") canvas.DrawPath(path, paint);
        else canvas.DrawText(line, 5 * scale, baseline, font, paint);
        baseline += font.Size * 1.4f;
    }
    using var image = SKImage.FromBitmap(bitmap);
    using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
    rows.Add(new
    {
        variant, dpi, landscape, repeat, width, height,
        family = typeface.FamilyName, edging = font.Edging.ToString(), hinting = font.Hinting.ToString(),
        font.Subpixel, font.LinearMetrics, font.EmbeddedBitmaps, font.BaselineSnap,
        lineWidths = lines.Select(line => font.MeasureText(line)).ToArray(),
        pathSha256 = Hash(Encoding.UTF8.GetBytes(string.Join('\n', paths))),
        pixelSha256 = Hash(bitmap.Bytes), encodedSha256 = Hash(encoded.ToArray()),
        sample = bitmap.GetPixel((int)(15 * scale), (int)(12 * scale)).ToString(),
    });
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    schemaVersion = 1, scope = "fixed-synthetic-native-font-probe", diagnosticOnly = true,
    productRendererExecuted = false, baselineApproved = false,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(), rows,
}));
return 0;

static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
