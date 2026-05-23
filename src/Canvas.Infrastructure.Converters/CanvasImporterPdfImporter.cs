using Canvas.Core.Contracts;
using Canvas.Importer.Document;
using Canvas.Importer.Graphics;

namespace Canvas.Infrastructure.Converters;

/// <summary>
/// Converts a PDF to a <see cref="DesignExportDto"/> using the Canvas.Importer low-level
/// PDF engine. The engine tokenizes, parses, and interprets the raw PDF content streams
/// into a typed scene graph (<see cref="PdfGraphicsElement"/> hierarchy) which is then
/// mapped to Canvas element DTOs.
/// </summary>
public static class CanvasImporterPdfImporter
{
    private const double HeaderZone = 0.08;
    private const double FooterZone = 0.92;

    // ── Entry point ───────────────────────────────────────────────────────────

    public static async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null)
    {
        var doc = await new Canvas.Importer.PdfImporter().LoadAsync(stream);

        var pages           = new List<PageDto>();
        var sharedByContent = new Dictionary<string, ElementDto>(StringComparer.Ordinal);
        bool multiPage      = doc.Pages.Count > 1;

        double canvasW = 595, canvasH = 842;

        int pageNum = 0;
        foreach (var page in doc.Pages)
        {
            pageNum++;
            int seq = 0;

            var mb      = page.MediaBox;
            canvasW     = mb?.Width  ?? 595;
            canvasH     = mb?.Height ?? 842;
            double originX = mb?.X ?? 0;
            double originY = mb?.Y ?? 0;
            double pageH   = originY + canvasH; // top of page in PDF coordinate space

            var elements = new List<ElementDto>();

            foreach (var el in page.GraphicsObjects)
            {
                if (el.IsDeleted) continue;
                EmitElement(el, originX, pageH, canvasH, pageNum, ref seq,
                    elements, multiPage, sharedByContent);
            }

            pages.Add(new PageDto { Id = $"page-{pageNum}", Elements = elements });
        }

        return new DesignExportDto
        {
            Id             = Guid.NewGuid().ToString("N")[..12],
            Name           = name ?? "Imported PDF",
            Pages          = pages,
            SharedElements = [.. sharedByContent.Values],
            PageSettings   = new PageSettingsDto
            {
                Width       = Math.Round(canvasW, 1),
                Height      = Math.Round(canvasH, 1),
                Orientation = canvasW > canvasH ? "landscape" : "portrait",
                Margins     = new MarginsDto { Top = 0, Right = 0, Bottom = 0, Left = 0 },
            },
        };
    }

    // ── Scene-graph walker ────────────────────────────────────────────────────

    private static void EmitElement(
        PdfGraphicsElement el,
        double originX, double pageH, double canvasH,
        int pg, ref int seq,
        List<ElementDto> elements,
        bool multiPage,
        Dictionary<string, ElementDto> sharedByContent)
    {
        switch (el)
        {
            case PdfTextElement txt:
            {
                var dto = MapText(txt, originX, pageH, pg, ref seq);
                if (dto is null) return;

                if (multiPage)
                {
                    string key    = (dto.Content ?? "").Trim();
                    bool isHeader = dto.Y < canvasH * HeaderZone;
                    bool isFooter = dto.Y > canvasH * FooterZone;
                    if ((isHeader || isFooter) && !string.IsNullOrWhiteSpace(key))
                    {
                        sharedByContent.TryAdd(key, dto);
                        return;
                    }
                }
                elements.Add(dto);
                break;
            }

            case PdfPathElement path:
            {
                var dto = MapPath(path, originX, pageH, pg, ref seq);
                if (dto is not null) elements.Add(dto);
                break;
            }

            case PdfImageElement img:
            {
                var dto = MapImage(img, originX, pageH, pg, ref seq);
                if (dto is not null) elements.Add(dto);
                break;
            }

            case PdfGroupElement grp:
                foreach (var child in grp.Children)
                {
                    if (!child.IsDeleted)
                        EmitElement(child, originX, pageH, canvasH, pg, ref seq,
                            elements, multiPage, sharedByContent);
                }
                break;

            case PdfShadingElement:
                break; // shadings are out of scope for the initial import adapter
        }
    }

    // ── Element mappers ───────────────────────────────────────────────────────

    private static ElementDto? MapText(PdfTextElement el, double originX, double pageH, int pg, ref int seq)
    {
        if (string.IsNullOrWhiteSpace(el.Text)) return null;

        // Rendered font size = Tf size × scale encoded in the transform matrix.
        // When PDFs use `Tf /F1 1; Tm 12 0 0 12 x y`, FontSize=1 but scale=12.
        double scale = Math.Sqrt(el.Transform.A * el.Transform.A + el.Transform.B * el.Transform.B);
        double fs    = scale > 0.01 ? el.FontSize * scale : (el.FontSize > 0 ? el.FontSize : 10);
        if (fs < 2) fs = 10; // guard against degenerate sizes

        double x = el.Transform.E - originX;

        // The D component of the element transform tells us the Y direction:
        //   D > 0 → Y increases upward (normal PDF space) → flip needed
        //   D < 0 → Y already increases downward (PDF used `cm 1 0 0 -1 0 H`) → no flip
        double y = el.Transform.D >= 0
            ? pageH - el.Transform.F - fs   // standard PDF Y (bottom-left origin)
            : el.Transform.F - fs;           // already in canvas Y (top-left origin)

        double w = Math.Max(el.Text.Length * fs * 0.6, 20);
        double h = fs * 1.5 + 4;

        return new ElementDto
        {
            Id      = $"txt-{pg}-{seq++}", Type = "text",
            X       = Math.Round(x, 1),   Y    = Math.Round(y, 1),
            Width   = Math.Round(w, 1),   Height = Math.Round(h, 1),
            Content = el.Text,
            Style   = new Dictionary<string, object>
            {
                ["fontSize"]   = Math.Round(fs, 1),
                ["fontFamily"] = CleanFont(el.FontResourceName),
                ["color"]      = ColorToHex(el.FillColor),
            },
        };
    }

    private static ElementDto? MapPath(PdfPathElement el, double originX, double pageH, int pg, ref int seq)
    {
        bool hasFill   = el.FillColor   != default;
        bool hasStroke = el.StrokeColor != default && el.LineWidth > 0;
        if (!hasFill && !hasStroke) return null;

        // Detect Y direction from the element transform (same logic as text)
        bool yIsDown = el.Transform.D < 0; // true when PDF already uses top-left coords

        double x, y, w, h;

        if (el.Bounds is PdfRectangle b)
        {
            x = b.X - originX;
            y = yIsDown ? b.Y : pageH - (b.Y + b.Height);
            w = b.Width;
            h = b.Height;
        }
        else
        {
            var (x1, y1, x2, y2) = SegmentsBounds(el.Segments);
            if (x1 == double.MaxValue) return null;
            x = x1 - originX;
            y = yIsDown ? y1 : pageH - y2;
            w = x2 - x1;
            h = y2 - y1;
        }

        if (w < 0.5 && h < 0.5) return null;

        string fill   = hasFill   ? ColorToHex(el.FillColor)   : "transparent";
        string stroke = hasStroke ? ColorToHex(el.StrokeColor) : "transparent";

        // Classify as thin line vs. filled shape
        bool isHLine = h < 3 && w > 5;
        bool isVLine = w < 3 && h > 5;

        if (isHLine || isVLine)
        {
            string lineColor = hasFill ? fill : stroke;
            return new ElementDto
            {
                Id     = $"ln-{pg}-{seq++}", Type   = "rect",
                X      = Math.Round(x, 1),   Y      = Math.Round(y, 1),
                Width  = Math.Max(1, Math.Round(w, 1)),
                Height = Math.Max(1, Math.Round(h, 1)),
                Style  = new Dictionary<string, object>
                    { ["backgroundColor"] = lineColor, ["borderWidth"] = 0 },
            };
        }

        return new ElementDto
        {
            Id     = $"sh-{pg}-{seq++}", Type   = "shape",
            X      = Math.Round(x, 1),   Y      = Math.Round(y, 1),
            Width  = Math.Max(1, Math.Round(w, 1)),
            Height = Math.Max(1, Math.Round(h, 1)),
            Style  = new Dictionary<string, object>
            {
                ["backgroundColor"] = fill,
                ["borderColor"]     = stroke,
                ["borderWidth"]     = (int)Math.Max(0, Math.Round(el.LineWidth)),
                ["borderStyle"]     = "solid",
            },
        };
    }

    private static ElementDto? MapImage(PdfImageElement el, double originX, double pageH, int pg, ref int seq)
    {
        if (el.ImageBytes.IsEmpty) return null;

        // Transform (A, B, C, D, E, F): for XObject images typically (±w, 0, 0, ±h, x, y)
        double imgW = Math.Abs(el.Transform.A);
        double imgH = Math.Abs(el.Transform.D);
        double imgX = el.Transform.E - originX;

        // D < 0 means the image is in a Y-flipped CTM (top-left origin already).
        // D > 0 means standard PDF bottom-left; F is bottom of image, so flip to get top.
        bool yIsDown = el.Transform.D < 0;
        double imgY  = yIsDown
            ? el.Transform.F
            : pageH - (el.Transform.F + el.Transform.D);

        if (imgW < 1 || imgH < 1) return null;

        return new ElementDto
        {
            Id      = $"img-{pg}-{seq++}", Type = "image",
            X       = Math.Max(0, Math.Round(imgX, 1)),
            Y       = Math.Max(0, Math.Round(imgY, 1)),
            Width   = Math.Round(imgW, 1),
            Height  = Math.Round(imgH, 1),
            Content = ImageBytesToDataUri(el.ImageBytes),
            Style   = new Dictionary<string, object> { ["fitMode"] = "contain" },
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (double x1, double y1, double x2, double y2) SegmentsBounds(
        List<PdfPathSegment> segments)
    {
        double x1 = double.MaxValue, y1 = double.MaxValue;
        double x2 = double.MinValue, y2 = double.MinValue;

        void Expand(double x, double y)
        {
            x1 = Math.Min(x1, x); y1 = Math.Min(y1, y);
            x2 = Math.Max(x2, x); y2 = Math.Max(y2, y);
        }

        foreach (var seg in segments)
        {
            switch (seg)
            {
                case MoveToSegment  m: Expand(m.Point.X, m.Point.Y); break;
                case LineToSegment  l: Expand(l.Point.X, l.Point.Y); break;
                case CurveToSegment c:
                    Expand(c.Control1.X, c.Control1.Y);
                    Expand(c.Control2.X, c.Control2.Y);
                    Expand(c.End.X,      c.End.Y); break;
                case RectangleSegment r:
                    Expand(r.Rectangle.X,                    r.Rectangle.Y);
                    Expand(r.Rectangle.X + r.Rectangle.Width, r.Rectangle.Y + r.Rectangle.Height);
                    break;
            }
        }

        return (x1, y1, x2, y2);
    }

    private static string ColorToHex(PdfColor c) => c.ColorSpace switch
    {
        PdfColorSpace.DeviceGray =>
            $"#{ToByte(c.C1):X2}{ToByte(c.C1):X2}{ToByte(c.C1):X2}",
        PdfColorSpace.DeviceRgb =>
            $"#{ToByte(c.C1):X2}{ToByte(c.C2):X2}{ToByte(c.C3):X2}",
        PdfColorSpace.DeviceCmyk =>
            $"#{ToByte((1 - c.C1) * (1 - c.C4)):X2}{ToByte((1 - c.C2) * (1 - c.C4)):X2}{ToByte((1 - c.C3) * (1 - c.C4)):X2}",
        _ => "#000000",
    };

    private static int ToByte(double v) => Math.Clamp((int)Math.Round(v * 255), 0, 255);

    private static string CleanFont(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Arial";
        var idx = name.IndexOf('+');
        return idx >= 0 ? name[(idx + 1)..] : name;
    }

    private static string ImageBytesToDataUri(ReadOnlyMemory<byte> bytes)
    {
        var span = bytes.Span;
        string mime = "image/png";
        if (span.Length >= 3 && span[0] == 0xFF && span[1] == 0xD8 && span[2] == 0xFF)
            mime = "image/jpeg";
        return $"data:{mime};base64,{Convert.ToBase64String(bytes.ToArray())}";
    }
}
