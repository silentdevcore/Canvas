using System.IO.Compression;
using Canvas.Core.Abstractions;
using Canvas.Core.Contracts;
using SkiaSharp;

namespace Canvas.Infrastructure.Converters;

public sealed class ImageDocumentExporter : IDocumentExporter
{
    private const float DefaultDpi = 150f;

    public string FormatKey     => "png";
    public string MimeType      => "image/png";
    public string FileExtension => ".png";
    public IExporterCapabilities Capabilities => new ExporterCapabilities(SupportsFormFields: false);

    public byte[] Export(DesignExportDto design) => Export(design, null);

    public byte[] Export(DesignExportDto design, ExportOptions? options)
    {
        var dpi   = options?.Dpi ?? DefaultDpi;
        var scale = dpi / 72f;
        var ps    = design.PageSettings ?? new PageSettingsDto();

        var pages = design.Pages.Count > 0
            ? design.Pages
            : [new PageDto { Id = "p1", Elements = design.SharedElements }];

        if (pages.Count == 1)
            return RenderPage(pages[0], design.SharedElements, ps, SKEncodedImageFormat.Png, 100, scale);

        // Multi-page → zip of PNGs
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (int i = 0; i < pages.Count; i++)
            {
                var png   = RenderPage(pages[i], design.SharedElements, ps, SKEncodedImageFormat.Png, 100, scale);
                var entry = zip.CreateEntry($"page-{i + 1}.png");
                using var es = entry.Open();
                es.Write(png, 0, png.Length);
            }
        }
        return zipStream.ToArray();
    }

    private static byte[] RenderPage(PageDto page, List<ElementDto> shared, PageSettingsDto ps,
        SKEncodedImageFormat format, int quality, float scale)
    {
        int bmpW = (int)(ps.Width  * scale);
        int bmpH = (int)(ps.Height * scale);

        using var bitmap  = new SKBitmap(bmpW, bmpH, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas  = new SKCanvas(bitmap);

        // Background
        var bgColor = ParseColor(ps.BackgroundColor ?? "#ffffff");
        canvas.Clear(bgColor);

        var elements = page.Elements
            .Concat(shared.Where(s => !page.Elements.Any(e => e.Id == s.Id)))
            .Where(e => e.Hidden != true)
            .ToList();

        foreach (var el in elements)
            DrawElement(canvas, el, scale);

        using var img  = SKImage.FromBitmap(bitmap);
        using var data = img.Encode(format, quality);
        return data.ToArray();
    }

    private static void DrawElement(SKCanvas canvas, ElementDto el, float scale)
    {
        var s = el.Style ?? [];

        float x = (float)(el.X  * scale);
        float y = (float)(el.Y  * scale);
        float w = (float)(el.Width  * scale);
        float h = (float)(el.Height * scale);

        var rect = new SKRect(x, y, x + w, y + h);

        var rotation = (float)s.GetNum("rotation", 0);
        if (rotation != 0)
        {
            canvas.Save();
            canvas.RotateDegrees(rotation, x + w / 2, y + h / 2);
        }

        switch (el.Type)
        {
            case "rect":
            case "shape":
            {
                var fill   = ParseColor(s.GetStr("backgroundColor", s.GetStr("fill", "transparent")));
                var radius = (float)s.GetNum("borderRadius", 0) * scale;
                using var paint = new SKPaint { Color = fill, IsAntialias = true };
                canvas.DrawRoundRect(rect, radius, radius, paint);
                DrawBorder(canvas, rect, s, radius, scale);
                break;
            }

            case "circle":
            {
                var fill = ParseColor(s.GetStr("backgroundColor", s.GetStr("fill", "transparent")));
                using var paint = new SKPaint { Color = fill, IsAntialias = true };
                canvas.DrawOval(rect, paint);
                DrawBorder(canvas, rect, s, w / 2, scale);
                break;
            }

            case "line":
            {
                var color = ParseColor(s.GetStr("backgroundColor", "#9ca3af"));
                using var paint = new SKPaint { Color = color, StrokeWidth = h, IsAntialias = true, IsStroke = true };
                canvas.DrawLine(x, y + h / 2, x + w, y + h / 2, paint);
                break;
            }

            case "text":
            case "link":
            case "number":
            {
                var text  = el.Type == "number" ? el.NumberValue?.ToString() ?? "" : el.Content ?? "";
                var color = ParseColor(s.GetStr("color", "#111827"));
                var fs    = (float)s.GetNum("fontSize", 14) * scale;
                var bold  = s.GetStr("fontWeight", "normal") is "bold" or "700" or "800" or "900";
                using var tf = SKTypeface.FromFamilyName(
                    s.GetStr("fontFamily", "Arial"),
                    bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                    SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                using var font  = new SKFont(tf, fs);
                using var paint = new SKPaint { Color = color, IsAntialias = true };
                canvas.DrawText(text, x, y + fs, font, paint);
                break;
            }

            case "table":
                DrawTable(canvas, el, x, y, w, h, scale);
                break;

            case "image":
            {
                var src = el.Content ?? "";
                byte[]? imgBytes = null;
                if (src.StartsWith("data:"))
                {
                    var base64 = src[(src.IndexOf(',') + 1)..];
                    try { imgBytes = Convert.FromBase64String(base64); } catch { }
                }
                else if (src.StartsWith("http://") || src.StartsWith("https://"))
                {
                    try { imgBytes = HttpImageCache.Fetch(src); } catch { }
                }
                if (imgBytes is not null)
                {
                    using var skData = SKData.CreateCopy(imgBytes);
                    using var img    = SKImage.FromEncodedData(skData);
                    if (img is not null)
                    {
                        using var paint = new SKPaint { IsAntialias = true };
                        canvas.DrawImage(img, rect, paint);
                    }
                }
                break;
            }

            case "signature":
            {
                var label = el.SignatureLabel ?? "Signature";
                var fs    = 10f * scale;
                using var font  = new SKFont(SKTypeface.Default, fs);
                using var paint = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
                canvas.DrawLine(x + 4, y + h - 14 * scale, x + w - 4, y + h - 14 * scale, paint);
                canvas.DrawText(label, x + 4, y + h - 4 * scale, font, paint);
                break;
            }
        }

        if (rotation != 0)
            canvas.Restore();
    }

    private static void DrawTable(SKCanvas canvas, ElementDto el, float x, float y, float w, float h, float scale)
    {
        var s        = el.Style ?? [];
        var cellData = el.CellData ?? [];
        if (cellData.Length == 0) return;

        var cols  = cellData[0]?.Length ?? 1;
        var rows  = cellData.Length;
        var cellW = w / cols;
        var cellH = h / rows;
        var bw    = (float)s.GetNum("borderWidth", 1);
        var bc    = ParseColor(s.GetStr("borderColor", "#000000"));

        var hasHdr = el.HeaderRow == true;
        var hdrBg  = ParseColor(el.HeaderBgColor ?? "#f1f5f9");

        for (int r = 0; r < rows; r++)
        {
            var row = cellData[r] ?? [];
            for (int c = 0; c < cols; c++)
            {
                var cx   = x + c * cellW;
                var cy   = y + r * cellH;
                var cell = row.Length > c ? row[c] : "";
                var isHdr = hasHdr && r == 0;

                if (isHdr)
                {
                    using var bgPaint = new SKPaint { Color = hdrBg };
                    canvas.DrawRect(cx, cy, cellW, cellH, bgPaint);
                }

                using var borderPaint = new SKPaint { Color = bc, StrokeWidth = bw, IsStroke = true };
                canvas.DrawRect(cx, cy, cellW, cellH, borderPaint);

                if (!string.IsNullOrEmpty(cell))
                {
                    var fs = 9f * scale;
                    using var font  = new SKFont(SKTypeface.Default, fs);
                    using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                    if (isHdr) font.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                    canvas.DrawText(cell ?? "", cx + 4, cy + cellH / 2 + fs / 2, font, paint);
                }
            }
        }
    }

    private static void DrawBorder(SKCanvas canvas, SKRect rect, Dictionary<string, object> s, float radius, float scale)
    {
        var bw = (float)s.GetNum("borderWidth", 0);
        if (bw <= 0) return;
        var bc    = ParseColor(s.GetStr("borderColor", "#000000"));
        var bs    = s.GetStr("borderStyle", "solid");
        using var paint = new SKPaint
        {
            Color      = bc,
            StrokeWidth = bw * scale,
            IsStroke   = true,
            IsAntialias = true,
            PathEffect = bs == "dashed" ? SKPathEffect.CreateDash([8, 4], 0)
                       : bs == "dotted" ? SKPathEffect.CreateDash([2, 4], 0)
                       : null,
        };
        canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    private static SKColor ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex == "transparent") return SKColors.Transparent;
        try { return SKColor.Parse(hex); }
        catch { return SKColors.Transparent; }
    }
}

/// <summary>JPEG variant — same render pipeline, different encode format.</summary>
public sealed class JpegDocumentExporter : IDocumentExporter
{
    public string FormatKey     => "jpeg";
    public string MimeType      => "image/jpeg";
    public string FileExtension => ".jpg";
    public IExporterCapabilities Capabilities => new ExporterCapabilities(SupportsFormFields: false);

    private const int DefaultQuality = 90;

    public byte[] Export(DesignExportDto design) => Export(design, null);

    public byte[] Export(DesignExportDto design, ExportOptions? options)
    {
        var quality = options?.Quality ?? DefaultQuality;
        var pngExporter = new ImageDocumentExporter();
        var pngBytes    = pngExporter.Export(design, options);

        using var skData  = SKData.CreateCopy(pngBytes);
        using var img     = SKImage.FromEncodedData(skData);
        if (img is null) return pngBytes;
        using var jpgData = img.Encode(SKEncodedImageFormat.Jpeg, quality);
        return jpgData.ToArray();
    }
}

internal static class HttpImageCache
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> _cache = new();

    public static byte[] Fetch(string url)
        => _cache.GetOrAdd(url, u => _http.GetByteArrayAsync(u).GetAwaiter().GetResult());
}
