using Canvas.Core.Contracts;
using Canvas.Importer.Analysis;
using Canvas.Importer.Document;
using Canvas.Importer.Graphics;

namespace Canvas.Infrastructure.Converters;

/// <summary>
/// Converts a PDF to a <see cref="DesignExportDto"/> using the Canvas.Importer low-level
/// PDF engine. The engine tokenizes, parses, and interprets the raw PDF content streams
/// into a typed scene graph and Phase 5 primitive analysis layer which is then mapped
/// to Canvas element DTOs.
/// </summary>
public static class CanvasImporterPdfImporter
{
    private const double HeaderZone = 0.08;
    private const double FooterZone = 0.92;

    // ── Entry point ───────────────────────────────────────────────────────────

    public static async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null)
    {
        var doc = await new Canvas.Importer.PdfImporter().LoadAsync(stream);
        var sceneGraphEngine = new SceneGraphEngine();

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

            var scenePage = sceneGraphEngine.BuildPage(pageNum - 1, page);
            var orderedPrimitives = OrderForImport(scenePage).ToList();
            var elements = new List<ElementDto>();

            foreach (var primitive in orderedPrimitives)
            {
                EmitPrimitive(primitive, originX, pageH, canvasH, pageNum, ref seq,
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

    // ── Scene-graph primitive walker ──────────────────────────────────────────

    private static void EmitPrimitive(
        PrimitiveObject primitive,
        double originX, double pageH, double canvasH,
        int pg, ref int seq,
        List<ElementDto> elements,
        bool multiPage,
        Dictionary<string, ElementDto> sharedByContent)
    {
        switch (primitive)
        {
            case PrimitiveText txt:
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

            case PrimitiveShape shape:
            {
                var dto = MapPath(shape, originX, pageH, pg, ref seq);
                if (dto is not null) elements.Add(dto);
                break;
            }

            case PrimitivePath path:
            {
                var dto = MapPath(path, originX, pageH, pg, ref seq);
                if (dto is not null) elements.Add(dto);
                break;
            }

            case PrimitiveImage img:
            {
                var dto = MapImage(img, originX, pageH, pg, ref seq);
                if (dto is not null) elements.Add(dto);
                break;
            }

            case PrimitiveGroup grp:
                foreach (var child in OrderChildrenForImport(grp.Children))
                {
                    EmitPrimitive(child, originX, pageH, canvasH, pg, ref seq,
                        elements, multiPage, sharedByContent);
                }
                break;
        }
    }

    // ── Element mappers ───────────────────────────────────────────────────────

    private static ElementDto? MapText(PrimitiveText el, double originX, double pageH, int pg, ref int seq)
    {
        if (string.IsNullOrWhiteSpace(el.Text)) return null;

        double scale = Math.Max(el.Transform.ScaleY, el.Transform.ScaleX);
        double fs    = scale > 0.01 ? el.FontSize * scale : (el.FontSize > 0 ? el.FontSize : el.Bounds.Height);
        if (fs < 2) fs = 10; // guard against degenerate sizes

        var (x, y, w, h) = TextCanvasFrame(el, originX, pageH);
        if (w < 1 || h < 1)
        {
            w = Math.Max(el.Text.Length * fs * 0.6, 20);
            h = fs * 1.5 + 4;
        }

        return new ElementDto
        {
            Id      = $"txt-{pg}-{seq++}", Type = "text",
            X       = Math.Round(x, 1),   Y    = Math.Round(y, 1),
            Width   = Math.Round(w, 1),   Height = Math.Round(h, 1),
            Content = el.Text,
            Style   = new Dictionary<string, object>
            {
                ["fontSize"]   = Math.Round(fs, 1),
                ["fontFamily"] = CleanFont(el.FontName ?? el.FontResourceName),
                ["color"]      = ColorToHex(el.GraphicsState.FillColor),
                ["rotation"]   = Math.Round(ToCanvasRotation(el.Geometry.RotationDegrees), 2),
                ["pdfClassification"] = el.Classification.ToString(),
            },
        };
    }

    private static ElementDto? MapPath(PrimitiveObject el, double originX, double pageH, int pg, ref int seq)
    {
        bool hasFill   = el.GraphicsState.FillColor != default;
        bool hasStroke = el.GraphicsState.StrokeColor != default && el.GraphicsState.LineWidth > 0;
        if (!hasFill && !hasStroke) return null;

        var (x, y, w, h) = ToCanvasBounds(el.Bounds, originX, pageH);

        if (w < 0.5 && h < 0.5) return null;

        string fill   = hasFill   ? ColorToHex(el.GraphicsState.FillColor)   : "transparent";
        string stroke = hasStroke ? ColorToHex(el.GraphicsState.StrokeColor) : "transparent";

        // Classify as thin line vs. filled shape
        bool isHLine = h < 3 && w > 5;
        bool isVLine = w < 3 && h > 5;
        bool isLine = isHLine || isVLine ||
            el.Classification is PrimitiveClassification.Separator or PrimitiveClassification.TableLine;

        if (isLine)
        {
            string lineColor = hasFill ? fill : stroke;
            return new ElementDto
            {
                Id     = $"ln-{pg}-{seq++}", Type   = "rect",
                X      = Math.Round(x, 1),   Y      = Math.Round(y, 1),
                Width  = Math.Max(1, Math.Round(w, 1)),
                Height = Math.Max(1, Math.Round(h, 1)),
                Style  = new Dictionary<string, object>
                {
                    ["backgroundColor"] = lineColor,
                    ["borderWidth"] = 0,
                    ["pdfClassification"] = el.Classification.ToString(),
                },
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
                ["borderWidth"]     = (int)Math.Max(0, Math.Round(el.GraphicsState.LineWidth)),
                ["borderStyle"]     = "solid",
                ["pdfClassification"] = el.Classification.ToString(),
            },
        };
    }

    private static ElementDto? MapImage(PrimitiveImage el, double originX, double pageH, int pg, ref int seq)
    {
        if (el.ImageBytes.IsEmpty) return null;

        var (imgX, imgY, imgW, imgH, rotation) = TransformedCanvasFrame(new PdfRectangle(0, 0, 1, 1), el.Transform, originX, pageH);

        if (imgW < 1 || imgH < 1) return null;

        return new ElementDto
        {
            Id      = $"img-{pg}-{seq++}", Type = "image",
            X       = Math.Max(0, Math.Round(imgX, 1)),
            Y       = Math.Max(0, Math.Round(imgY, 1)),
            Width   = Math.Round(imgW, 1),
            Height  = Math.Round(imgH, 1),
            Content = ImageBytesToDataUri(el.ImageBytes),
            Style   = new Dictionary<string, object>
            {
                ["fitMode"] = "contain",
                ["rotation"] = Math.Round(rotation, 2),
                ["pdfClassification"] = el.Classification.ToString(),
            },
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IEnumerable<PrimitiveObject> OrderForImport(PdfScenePage scenePage)
    {
        var textOrder = new Dictionary<PrimitiveObject, int>();
        var order = 0;
        foreach (var text in scenePage.ReadingOrder?.Lines.SelectMany(static line => line.Texts) ?? [])
        {
            textOrder.TryAdd(text, order++);
        }

        return scenePage.Layers
            .SelectMany(static layer => layer.Objects)
            .SelectMany(FlattenPrimitive)
            .Where(static primitive => primitive.Kind != PrimitiveKind.Group)
            .OrderBy(static primitive => primitive.Kind == PrimitiveKind.Text ? 1 : 0)
            .ThenBy(primitive => textOrder.TryGetValue(primitive, out var index) ? index : primitive.ZOrder)
            .ThenBy(static primitive => primitive.ZOrder);
    }

    private static IEnumerable<PrimitiveObject> OrderChildrenForImport(IEnumerable<PrimitiveObject> children)
    {
        return children
            .SelectMany(FlattenPrimitive)
            .Where(static primitive => primitive.Kind != PrimitiveKind.Group)
            .OrderBy(static primitive => primitive.ZOrder);
    }

    private static IEnumerable<PrimitiveObject> FlattenPrimitive(PrimitiveObject primitive)
    {
        yield return primitive;
        foreach (var child in primitive.Children.SelectMany(FlattenPrimitive))
        {
            yield return child;
        }
    }

    private static (double x, double y, double width, double height) ToCanvasBounds(
        PdfRectangle bounds,
        double originX,
        double pageH)
    {
        var x = bounds.Left - originX;
        var y = pageH - bounds.Top;
        var width = bounds.Right - bounds.Left;
        var height = bounds.Top - bounds.Bottom;
        return (x, y, width, height);
    }

    private static (double x, double y, double width, double height) TextCanvasFrame(
        PrimitiveText text,
        double originX,
        double pageH)
    {
        var fontSize = Math.Max(text.FontSize, 1d);
        var localWidth = Math.Max(fontSize * 0.35d, text.Text.Length * fontSize * 0.5d);
        var localBounds = new PdfRectangle(0, -fontSize * 0.2d, localWidth, fontSize);
        var (x, y, width, height, _) = TransformedCanvasFrame(localBounds, text.Transform, originX, pageH);
        return (x, y, width, height);
    }

    private static (double x, double y, double width, double height, double rotation) TransformedCanvasFrame(
        PdfRectangle localBounds,
        PdfMatrix transform,
        double originX,
        double pageH)
    {
        var center = MatrixEngine.TransformPoint(new PdfPoint(localBounds.CenterX, localBounds.CenterY), transform);
        var width = Math.Abs(localBounds.Width) * Math.Max(transform.ScaleX, 0.01d);
        var height = Math.Abs(localBounds.Height) * Math.Max(transform.ScaleY, 0.01d);
        var canvasCenterX = center.X - originX;
        var canvasCenterY = pageH - center.Y;

        return (
            canvasCenterX - width / 2d,
            canvasCenterY - height / 2d,
            width,
            height,
            ToCanvasRotation(MatrixEngine.ExtractRotationDegrees(transform)));
    }

    private static double ToCanvasRotation(double pdfDegrees)
    {
        var degrees = -pdfDegrees % 360d;
        if (degrees <= -180d) degrees += 360d;
        if (degrees > 180d) degrees -= 360d;
        return Math.Abs(degrees) < 0.01d ? 0d : degrees;
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
