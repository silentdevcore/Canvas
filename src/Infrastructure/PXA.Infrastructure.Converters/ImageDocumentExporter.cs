using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PXA.Core.Abstractions;
using PXA.Core.Contracts;
using SkiaSharp;

namespace PXA.Infrastructure.Converters;

public sealed class ImageDocumentExporter : DocumentExporter
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

    internal static byte[] RenderPage(PageDto page, List<ElementDto> shared, PageSettingsDto ps,
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
                var bold  = s.GetStr("fontWeight", "normal") is "bold" or "600" or "700" or "800" or "900";
                using var tf = SKTypeface.FromFamilyName(
                    s.GetStr("fontFamily", "Arial"),
                    bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                    SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                using var font  = new SKFont(tf, fs);
                using var paint = new SKPaint { Color = color, IsAntialias = true };
                DrawTextBlock(canvas, text, s, x, y, w, font, paint, scale);
                break;
            }

            case "richtext":
            {
                var color = ParseColor(s.GetStr("color", "#111827"));
                var fs    = (float)s.GetNum("fontSize", 13) * scale;
                var bold  = s.GetStr("fontWeight", "normal") is "bold" or "600" or "700" or "800" or "900";
                using var tf = SKTypeface.FromFamilyName(
                    s.GetStr("fontFamily", "Arial"),
                    bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                    SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                using var font  = new SKFont(tf, fs);
                using var paint = new SKPaint { Color = color, IsAntialias = true };
                DrawTextBlock(canvas, HtmlToText(el.HtmlContent ?? el.Content ?? ""), s, x, y, w, font, paint, scale);
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

        var cols          = cellData[0]?.Length ?? 1;
        var rows          = cellData.Length;
        var matrixHeaders = RdlMatrixHeaders(s);
        var visualRows    = rows + matrixHeaders.Count;
        var cellW         = w / cols;
        var cellH         = h / Math.Max(visualRows, 1);
        var bw            = (float)s.GetNum("borderWidth", 1);
        var bc            = ParseColor(s.GetStr("borderColor", "#000000"));

        var hasHdr = el.HeaderRow == true;
        var hdrBg  = ParseColor(el.HeaderBgColor ?? "#f1f5f9");
        var matrixHeaderBg = ParseColor("#e0f2fe");
        var matrixHeaderText = ParseColor("#075985");

        using (var headerTf = SKTypeface.FromFamilyName(
            "Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright))
        using (var headerFont = new SKFont(headerTf, 10f * scale))
        using (var headerPaint = new SKPaint { Color = matrixHeaderText, IsAntialias = true })
        {
            for (var hIndex = 0; hIndex < matrixHeaders.Count; hIndex++)
            {
                var cy = y + hIndex * cellH;
                using var bgPaint = new SKPaint { Color = matrixHeaderBg };
                using var borderPaint = new SKPaint { Color = bc, StrokeWidth = bw, IsStroke = true };
                canvas.DrawRect(x, cy, w, cellH, bgPaint);
                canvas.DrawRect(x, cy, w, cellH, borderPaint);

                var pad     = 4f * scale;
                var lines   = WrapText(matrixHeaders[hIndex], Math.Max(1, w - 2 * pad), headerFont);
                var lineGap = headerFont.Size * 1.25f;
                var startY  = cy + Math.Max(headerFont.Size + pad, (cellH - lines.Count * lineGap) / 2 + headerFont.Size);
                for (int li = 0; li < lines.Count; li++)
                    canvas.DrawText(lines[li], x + pad, startY + li * lineGap, headerFont, headerPaint);
            }
        }

        // Sparse per-cell styling (background / borders / alignment); unset cells keep table defaults.
        var cellStyles = (el.CellStyles ?? []).GroupBy(cs => (cs.Row, cs.Col)).ToDictionary(g => g.Key, g => g.First());

        for (int r = 0; r < rows; r++)
        {
            var row = cellData[r] ?? [];
            for (int c = 0; c < cols; c++)
            {
                var cx   = x + c * cellW;
                var cy   = y + (r + matrixHeaders.Count) * cellH;
                var cell = row.Length > c ? row[c] : "";
                var isHdr = hasHdr && r == 0;
                cellStyles.TryGetValue((r, c), out var cstyle);

                if (cstyle?.BackgroundColor is { } cellBg)
                {
                    using var bgPaint = new SKPaint { Color = ParseColor(cellBg) };
                    canvas.DrawRect(cx, cy, cellW, cellH, bgPaint);
                }
                else if (isHdr)
                {
                    using var bgPaint = new SKPaint { Color = hdrBg };
                    canvas.DrawRect(cx, cy, cellW, cellH, bgPaint);
                }

                if (cstyle is not null && HasCellBorder(cstyle))
                    DrawCellBorders(canvas, cstyle, cx, cy, cellW, cellH);
                else
                {
                    using var borderPaint = new SKPaint { Color = bc, StrokeWidth = bw, IsStroke = true };
                    canvas.DrawRect(cx, cy, cellW, cellH, borderPaint);
                }

                if (!string.IsNullOrEmpty(cell))
                {
                    var fs = (cstyle?.FontSize is { } cfs ? (float)cfs : 9f) * scale;
                    var bold = isHdr || cstyle?.Bold == true;
                    var tf = SKTypeface.FromFamilyName(
                        cstyle?.FontFamily ?? (isHdr ? "Arial" : null),
                        bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                        SKFontStyleWidth.Normal,
                        cstyle?.Italic == true ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright) ?? SKTypeface.Default;
                    using var font  = new SKFont(tf, fs);
                    using var paint = new SKPaint { Color = ParseColor(cstyle?.Color ?? "#000000"), IsAntialias = true };

                    var pad     = (cstyle?.Padding is { } cpad ? (float)cpad : 4f) * scale;
                    var lines   = WrapText(cell ?? "", Math.Max(1, cellW - 2 * pad), font);
                    var lineGap = fs * 1.25f;
                    // Vertically centre the wrapped block within the cell.
                    var startY  = cy + Math.Max(fs + pad, (cellH - lines.Count * lineGap) / 2 + fs);
                    for (int li = 0; li < lines.Count; li++)
                    {
                        var lineW = font.MeasureText(lines[li]);
                        var tx = cstyle?.TextAlign switch
                        {
                            "center" => cx + (cellW - lineW) / 2,
                            "right"  => cx + cellW - pad - lineW,
                            _        => cx + pad
                        };
                        canvas.DrawText(lines[li], tx, startY + li * lineGap, font, paint);
                    }
                }
            }
        }
    }

    private static bool HasCellBorder(CellStyleDto cs) =>
        cs.BorderColor is not null || cs.BorderWidth is not null
        || cs.BorderTop is not null || cs.BorderRight is not null
        || cs.BorderBottom is not null || cs.BorderLeft is not null;

    // Uniform border (full rect) first, then any per-side overrides drawn on top.
    private static void DrawCellBorders(SKCanvas canvas, CellStyleDto cs, float cx, float cy, float cw, float ch)
    {
        if (cs.BorderColor is not null || cs.BorderWidth is not null)
        {
            using var p = new SKPaint { Color = ParseColor(cs.BorderColor ?? "#000000"), StrokeWidth = (float)(cs.BorderWidth ?? 1), IsStroke = true };
            canvas.DrawRect(cx, cy, cw, ch, p);
        }
        DrawCellSide(canvas, cs.BorderTop,    cx,      cy,      cx + cw, cy);
        DrawCellSide(canvas, cs.BorderRight,  cx + cw, cy,      cx + cw, cy + ch);
        DrawCellSide(canvas, cs.BorderBottom, cx,      cy + ch, cx + cw, cy + ch);
        DrawCellSide(canvas, cs.BorderLeft,   cx,      cy,      cx,      cy + ch);
    }

    private static void DrawCellSide(SKCanvas canvas, CellBorderSideDto? side, float x1, float y1, float x2, float y2)
    {
        if (side is null) return;
        using var p = new SKPaint { Color = ParseColor(side.Color ?? "#000000"), StrokeWidth = (float)(side.Width ?? 1), IsStroke = true };
        canvas.DrawLine(x1, y1, x2, y2, p);
    }

    private static List<string> RdlMatrixHeaders(Dictionary<string, object> style)
    {
        var headers = new List<string>();
        AddRdlMatrixHeaders(style, "rdlTablixColumnHierarchy", headers);
        AddRdlMatrixHeaders(style, "rdlTablixRowHierarchy", headers);
        return headers;
    }

    private static void AddRdlMatrixHeaders(Dictionary<string, object> style, string key, List<string> headers)
    {
        if (!style.TryGetValue(key, out var value) || value is null) return;

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            foreach (var item in jsonArray.EnumerateArray())
                AddRdlMatrixHeader(item, headers);
            return;
        }

        if (value is IEnumerable<object> items)
        {
            foreach (var item in items)
                AddRdlMatrixHeader(item, headers);
        }
    }

    private static void AddRdlMatrixHeader(object item, List<string> headers)
    {
        switch (item)
        {
            case JsonElement { ValueKind: JsonValueKind.Object } json:
                var text = JsonProp(json, "headerText") ?? JsonProp(json, "groupName");
                if (!string.IsNullOrWhiteSpace(text)) headers.Add(text);
                break;
            case IReadOnlyDictionary<string, object> dict:
                if ((HeaderValue(dict, "headerText") ?? HeaderValue(dict, "groupName")) is { Length: > 0 } value)
                    headers.Add(value);
                break;
        }
    }

    private static string? JsonProp(JsonElement json, string name) =>
        json.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string? HeaderValue(IReadOnlyDictionary<string, object> dict, string key) =>
        dict.TryGetValue(key, out var value) ? value?.ToString() : null;

    /// <summary>
    /// Draws word-wrapped text within an element box, honouring padding, line height and
    /// horizontal alignment — mirroring how the editor lays text out in its element div.
    /// Coordinates (<paramref name="x"/>, <paramref name="y"/>, <paramref name="w"/>) are
    /// already DPI-scaled; padding values from the style are scaled here.
    /// </summary>
    private static void DrawTextBlock(SKCanvas canvas, string text, Dictionary<string, object> s,
        float x, float y, float w, SKFont font, SKPaint paint, float scale)
    {
        var padL       = (float)s.GetNum("paddingLeft", 0) * scale;
        var padR       = (float)s.GetNum("paddingRight", 0) * scale;
        var padT       = (float)s.GetNum("paddingTop", 0) * scale;
        var lineHeight = (float)s.GetNum("lineHeight", 1.4);
        var align      = s.GetStr("textAlign", "left");

        var maxW  = Math.Max(1, w - padL - padR);
        var lines = WrapText(text, maxW, font);

        var baseline = y + padT + font.Size;
        foreach (var line in lines)
        {
            var drawX = align switch
            {
                "center"         => x + padL + (maxW - font.MeasureText(line)) / 2,
                "right" or "end" => x + w - padR - font.MeasureText(line),
                _                => x + padL,
            };
            canvas.DrawText(line, drawX, baseline, font, paint);
            baseline += font.Size * lineHeight;
        }
    }

    /// <summary>Word-wraps text to <paramref name="maxWidth"/> using font metrics, honouring
    /// explicit newlines and breaking over-long words by character.</summary>
    private static List<string> WrapText(string text, float maxWidth, SKFont font)
    {
        var lines = new List<string>();
        foreach (var rawLine in (text ?? "").Replace("\r", "").Split('\n'))
        {
            if (rawLine.Length == 0) { lines.Add(""); continue; }

            var cur = new StringBuilder();
            foreach (var word in rawLine.Split(' '))
            {
                var candidate = cur.Length == 0 ? word : $"{cur} {word}";
                if (cur.Length == 0 || font.MeasureText(candidate) <= maxWidth)
                {
                    cur.Clear().Append(candidate);
                    continue;
                }

                lines.Add(cur.ToString());
                cur.Clear();

                if (font.MeasureText(word) > maxWidth)
                {
                    foreach (var ch in word)
                    {
                        if (cur.Length > 0 && font.MeasureText($"{cur}{ch}") > maxWidth)
                        {
                            lines.Add(cur.ToString());
                            cur.Clear();
                        }
                        cur.Append(ch);
                    }
                }
                else
                {
                    cur.Append(word);
                }
            }
            if (cur.Length > 0) lines.Add(cur.ToString());
        }

        if (lines.Count == 0) lines.Add("");
        return lines;
    }

    /// <summary>Flattens rich-text HTML to plain text, preserving block/line breaks.</summary>
    private static string HtmlToText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</(p|div|li|tr|h[1-6])\s*>", "\n", RegexOptions.IgnoreCase);
        var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", ""));
        text = Regex.Replace(text, @"[ \t\f\v]+", " ");
        text = Regex.Replace(text, @" *\n *", "\n");
        return text.Trim();
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
public sealed class JpegDocumentExporter : DocumentExporter
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
