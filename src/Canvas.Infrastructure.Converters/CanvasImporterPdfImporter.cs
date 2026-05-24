using System.Text;
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
        var emittedFontFaces = new HashSet<string>(StringComparer.Ordinal);
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
                    elements, multiPage, sharedByContent, emittedFontFaces);
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
        Dictionary<string, ElementDto> sharedByContent,
        HashSet<string> emittedFontFaces)
    {
        switch (primitive)
        {
            case PrimitiveText txt:
            {
                var dto = MapText(txt, originX, pageH, pg, ref seq, emittedFontFaces);
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
                if (!EmitRectangularSubpaths(path, originX, pageH, pg, ref seq, elements) &&
                    !EmitCurvePath(path, originX, pageH, pg, ref seq, elements))
                {
                    var dto = MapPath(path, originX, pageH, pg, ref seq);
                    if (dto is not null) elements.Add(dto);
                }
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
                        elements, multiPage, sharedByContent, emittedFontFaces);
                }
                break;
        }
    }

    // ── Element mappers ───────────────────────────────────────────────────────

    private static ElementDto? MapText(
        PrimitiveText el,
        double originX,
        double pageH,
        int pg,
        ref int seq,
        HashSet<string> emittedFontFaces)
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

        var font = ResolveCssFont(el, emittedFontFaces);
        var isBold = el.Bold || font.Bold;
        var isItalic = el.Italic || font.Italic;

        var style = new Dictionary<string, object>
        {
            ["fontSize"]   = Math.Round(fs, 1),
            ["fontFamily"] = font.Family,
            ["fontWeight"] = isBold ? "bold" : "normal",
            ["fontStyle"]  = isItalic ? "italic" : "normal",
            ["color"]      = ColorToHex(el.GraphicsState.FillColor),
            ["rotation"]   = Math.Round(ToCanvasRotation(el.Geometry.RotationDegrees), 2),
            ["pdfFontName"] = font.PdfName,
            ["pdfClassification"] = el.Classification.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(font.DataUri) && !string.IsNullOrWhiteSpace(font.Format))
        {
            style["fontDataUri"] = font.DataUri;
            style["fontFormat"] = font.Format;
            style["fontDisplayName"] = font.DisplayName ?? font.Family;
        }

        return new ElementDto
        {
            Id      = $"txt-{pg}-{seq++}", Type = "text",
            X       = Math.Round(x, 1),   Y    = Math.Round(y, 1),
            Width   = Math.Round(w, 1),   Height = Math.Round(h, 1),
            Content = el.Text,
            Style = style,
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
        var (imgX, imgY, imgW, imgH, rotation) = TransformedCanvasFrame(new PdfRectangle(0, 0, 1, 1), el.Transform, originX, pageH);

        if (imgW < 1 || imgH < 1) return null;

        // Unsupported codec (JBIG2, JPEG2000, etc.) — emit a visible placeholder
        if (el.ImageBytes.IsEmpty)
        {
            return new ElementDto
            {
                Id     = $"img-{pg}-{seq++}", Type   = "shape",
                X      = Math.Max(0, Math.Round(imgX, 1)),
                Y      = Math.Max(0, Math.Round(imgY, 1)),
                Width  = Math.Round(imgW, 1),
                Height = Math.Round(imgH, 1),
                Style  = new Dictionary<string, object>
                {
                    ["backgroundColor"]   = "#f5f5f5",
                    ["borderColor"]       = "#bbbbbb",
                    ["borderWidth"]       = 1,
                    ["borderStyle"]       = "dashed",
                    ["pdfClassification"] = "UnsupportedImage",
                },
            };
        }

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
                ["fitMode"]           = "contain",
                ["rotation"]          = Math.Round(rotation, 2),
                ["pdfClassification"] = el.Classification.ToString(),
            },
        };
    }

    // ── Complex path splitter ─────────────────────────────────────────────────

    /// <summary>
    /// Splits a multi-subpath PrimitivePath (e.g. a QR code drawn with many `re` ops) into
    /// individual rect elements. Each sub-rectangle's local coordinates are converted to
    /// global PDF space via the path's CTM before mapping to canvas coordinates.
    /// Returns false when the path has ≤1 sub-rectangle, deferring to MapPath.
    /// </summary>
    private static bool EmitRectangularSubpaths(
        PrimitivePath el,
        double originX, double pageH,
        int pg, ref int seq,
        List<ElementDto> elements)
    {
        var localRects      = new List<PdfRectangle>();
        var subpathPoints   = new List<PdfPoint>();
        bool subpathHasCurve = false;

        foreach (var seg in el.Segments)
        {
            switch (seg)
            {
                case RectangleSegment rs:
                    localRects.Add(rs.Rectangle);
                    break;
                case MoveToSegment mt:
                    CommitSubpath(subpathPoints, subpathHasCurve, localRects);
                    subpathPoints.Clear();
                    subpathHasCurve = false;
                    subpathPoints.Add(mt.Point);
                    break;
                case LineToSegment lt:
                    if (!subpathHasCurve) subpathPoints.Add(lt.Point);
                    break;
                case CurveToSegment:
                    subpathHasCurve = true;
                    break;
                case ClosePathSegment:
                    if (!subpathHasCurve) CommitSubpath(subpathPoints, subpathHasCurve, localRects);
                    subpathPoints.Clear();
                    subpathHasCurve = false;
                    break;
            }
        }
        CommitSubpath(subpathPoints, subpathHasCurve, localRects);

        if (localRects.Count <= 1) return false;

        bool hasFill   = el.GraphicsState.FillColor   != default;
        bool hasStroke = el.GraphicsState.StrokeColor != default && el.GraphicsState.LineWidth > 0;
        if (!hasFill && !hasStroke) return false;

        string fill   = hasFill   ? ColorToHex(el.GraphicsState.FillColor)   : "transparent";
        string stroke = hasStroke ? ColorToHex(el.GraphicsState.StrokeColor) : "transparent";

        foreach (var localRect in localRects)
        {
            // localRect is in path-user space; apply CTM to get global PDF coords
            var globalRect = MatrixEngine.TransformBounds(localRect, el.Transform);
            var (x, y, w, h) = ToCanvasBounds(globalRect, originX, pageH);
            if (w < 0.1 || h < 0.1) continue;

            elements.Add(new ElementDto
            {
                Id     = $"sh-{pg}-{seq++}", Type   = "shape",
                X      = Math.Round(x, 1),   Y      = Math.Round(y, 1),
                Width  = Math.Max(0.5, Math.Round(w, 1)),
                Height = Math.Max(0.5, Math.Round(h, 1)),
                Style  = new Dictionary<string, object>
                {
                    ["backgroundColor"]   = fill,
                    ["borderColor"]       = stroke,
                    ["borderWidth"]       = (int)Math.Max(0, Math.Round(el.GraphicsState.LineWidth)),
                    ["borderStyle"]       = "solid",
                    ["pdfClassification"] = el.Classification.ToString(),
                },
            });
        }
        return true;
    }

    private static void CommitSubpath(List<PdfPoint> points, bool hasCurve, List<PdfRectangle> rects)
    {
        if (hasCurve || points.Count < 3) return;
        var left   = points.Min(static p => p.X);
        var right  = points.Max(static p => p.X);
        var bottom = points.Min(static p => p.Y);
        var top    = points.Max(static p => p.Y);
        var w = right - left;
        var h = top - bottom;
        if (w >= 0.01 && h >= 0.01)
            rects.Add(new PdfRectangle(left, bottom, w, h));
    }

    // ── Curve path → SVG data-URI image ──────────────────────────────────────

    /// <summary>
    /// Emits a PrimitivePath that contains Bézier curves as an SVG embedded in an image element.
    /// Path segment coordinates (pre-CTM) are mapped through the full CTM and Y-flipped into
    /// canvas space so the SVG viewBox can simply match the element's width×height.
    /// Returns false when the path contains no curves (defer to other handlers).
    /// </summary>
    private static bool EmitCurvePath(
        PrimitivePath el,
        double originX, double pageH,
        int pg, ref int seq,
        List<ElementDto> elements)
    {
        if (!el.Segments.Any(static s => s is CurveToSegment)) return false;

        bool hasFill   = el.GraphicsState.FillColor   != default;
        bool hasStroke = el.GraphicsState.StrokeColor != default && el.GraphicsState.LineWidth > 0;
        if (!hasFill && !hasStroke) return false;

        var (ex, ey, ew, eh) = ToCanvasBounds(el.Bounds, originX, pageH);
        if (ew < 0.5 || eh < 0.5) return false;

        // Build SVG path d — segment coordinates are in local (pre-CTM) space;
        // we transform each point to canvas space and offset by the element origin.
        var sb = new StringBuilder();
        foreach (var seg in el.Segments)
        {
            switch (seg)
            {
                case MoveToSegment mt:
                    var (mx, my) = LocalToSvg(mt.Point, el.Transform, originX, pageH, ex, ey);
                    sb.Append($"M {mx:F2} {my:F2} ");
                    break;
                case LineToSegment lt:
                    var (lx, ly) = LocalToSvg(lt.Point, el.Transform, originX, pageH, ex, ey);
                    sb.Append($"L {lx:F2} {ly:F2} ");
                    break;
                case CurveToSegment ct:
                    var (c1x, c1y) = LocalToSvg(ct.Control1, el.Transform, originX, pageH, ex, ey);
                    var (c2x, c2y) = LocalToSvg(ct.Control2, el.Transform, originX, pageH, ex, ey);
                    var (cex, cey) = LocalToSvg(ct.End,      el.Transform, originX, pageH, ex, ey);
                    sb.Append($"C {c1x:F2} {c1y:F2} {c2x:F2} {c2y:F2} {cex:F2} {cey:F2} ");
                    break;
                case RectangleSegment rs:
                    // Expand re into M/L/Z so SVG handles it
                    var r = rs.Rectangle;
                    var (rx0, ry0) = LocalToSvg(new PdfPoint(r.Left,  r.Bottom), el.Transform, originX, pageH, ex, ey);
                    var (rx1, ry1) = LocalToSvg(new PdfPoint(r.Right, r.Bottom), el.Transform, originX, pageH, ex, ey);
                    var (rx2, ry2) = LocalToSvg(new PdfPoint(r.Right, r.Top),    el.Transform, originX, pageH, ex, ey);
                    var (rx3, ry3) = LocalToSvg(new PdfPoint(r.Left,  r.Top),    el.Transform, originX, pageH, ex, ey);
                    sb.Append($"M {rx0:F2} {ry0:F2} L {rx1:F2} {ry1:F2} L {rx2:F2} {ry2:F2} L {rx3:F2} {ry3:F2} Z ");
                    break;
                case ClosePathSegment:
                    sb.Append("Z ");
                    break;
            }
        }

        var d = sb.ToString().Trim();
        if (d.Length == 0) return false;

        string fill   = hasFill   ? ColorToHex(el.GraphicsState.FillColor)   : "none";
        string stroke = hasStroke ? ColorToHex(el.GraphicsState.StrokeColor) : "none";
        double sw     = hasStroke ? el.GraphicsState.LineWidth : 0;

        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {ew:F2} {eh:F2}\">" +
                  $"<path d=\"{d}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{sw:F2}\" fill-rule=\"evenodd\"/>" +
                  "</svg>";

        var dataUri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

        var rotation = ToCanvasRotation(MatrixEngine.ExtractRotationDegrees(el.Transform));

        elements.Add(new ElementDto
        {
            Id      = $"svg-{pg}-{seq++}", Type = "image",
            X       = Math.Round(ex, 1),   Y    = Math.Round(ey, 1),
            Width   = Math.Round(ew, 1),   Height = Math.Round(eh, 1),
            Content = dataUri,
            Style   = new Dictionary<string, object>
            {
                ["fitMode"]           = "fill",
                ["rotation"]          = Math.Round(rotation, 2),
                ["pdfClassification"] = el.Classification.ToString(),
            },
        });
        return true;
    }

    private static (double x, double y) LocalToSvg(
        PdfPoint localPt,
        PdfMatrix ctm,
        double originX, double pageH,
        double elementX, double elementY)
    {
        var global = MatrixEngine.TransformPoint(localPt, ctm);
        var cx = global.X - originX - elementX;
        var cy = pageH - global.Y - elementY;
        return (cx, cy);
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

    private static CssFontInfo ResolveCssFont(PrimitiveText text, HashSet<string> emittedFontFaces)
    {
        var name = text.FontName ?? text.FontResourceName;
        var pdfName = CleanPdfFontName(name);
        if (string.IsNullOrWhiteSpace(pdfName))
        {
            return new CssFontInfo("Arial", "Arial", Bold: false, Italic: false);
        }

        var bold = IsBoldFontName(pdfName);
        var italic = IsItalicFontName(pdfName);
        var family = NormalizeCssFontFamily(pdfName);
        string? dataUri = null;
        string? format = null;
        string? displayName = null;

        if (!text.EmbeddedFontBytes.IsEmpty &&
            !string.IsNullOrWhiteSpace(text.EmbeddedFontFormat) &&
            !string.IsNullOrWhiteSpace(text.EmbeddedFontMimeType))
        {
            family = CreateImportedFontFamily(pdfName);
            displayName = NormalizeCssFontFamily(pdfName);
            var key = $"{family}:{text.EmbeddedFontFormat}:{text.EmbeddedFontBytes.Length}";
            if (emittedFontFaces.Add(key))
            {
                dataUri = CreateFontDataUri(text.EmbeddedFontBytes, text.EmbeddedFontMimeType);
                format = text.EmbeddedFontFormat;
            }
        }

        return new CssFontInfo(family, pdfName, bold, italic, dataUri, format, displayName);
    }

    private static string CleanPdfFontName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Arial";
        var clean = name.Trim('\'', '"').Replace('_', ' ');
        var plus = clean.IndexOf('+');
        return plus == 6 ? clean[(plus + 1)..] : clean;
    }

    private static string NormalizeCssFontFamily(string pdfName)
    {
        var family = pdfName;
        var comma = family.IndexOf(',');
        if (comma > 0)
        {
            family = family[..comma];
        }

        var dash = family.IndexOf('-');
        if (dash > 0)
        {
            family = family[..dash];
        }

        var compact = family.Replace(" ", string.Empty, StringComparison.Ordinal);
        var lower = compact.ToLowerInvariant();

        if (lower.StartsWith("timesnewroman", StringComparison.Ordinal) ||
            lower is "times" or "timesroman")
        {
            return "Times New Roman";
        }

        if (lower.StartsWith("helveticaneue", StringComparison.Ordinal))
        {
            return "Helvetica Neue";
        }

        if (lower.StartsWith("helvetica", StringComparison.Ordinal))
        {
            return "Helvetica";
        }

        if (lower.StartsWith("arial", StringComparison.Ordinal))
        {
            return "Arial";
        }

        if (lower.StartsWith("couriernew", StringComparison.Ordinal) ||
            lower.StartsWith("courier", StringComparison.Ordinal))
        {
            return "Courier New";
        }

        if (lower.StartsWith("segoeui", StringComparison.Ordinal))
        {
            return "Segoe UI";
        }

        if (lower.StartsWith("myriadpro", StringComparison.Ordinal))
        {
            return "Myriad Pro";
        }

        if (lower.StartsWith("minionpro", StringComparison.Ordinal))
        {
            return "Minion Pro";
        }

        if (lower.StartsWith("symbol", StringComparison.Ordinal))
        {
            return "Symbol";
        }

        if (lower.StartsWith("zapfdingbats", StringComparison.Ordinal))
        {
            return "Zapf Dingbats";
        }

        family = StripFontVendorSuffix(family);
        compact = family.Replace(" ", string.Empty, StringComparison.Ordinal);
        lower = compact.ToLowerInvariant();

        return family.Length == 0 ? "Arial" : family;
    }

    private static string StripFontVendorSuffix(string family)
    {
        var suffixes = new[]
        {
            "PSMT", "PS", "MT", "MS", "LTStd", "LTPro", "Std", "Pro",
            "Regular", "Roman", "Book", "Medium", "Normal"
        };

        var clean = family.Trim();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var suffix in suffixes)
            {
                if (clean.Length > suffix.Length &&
                    clean.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean[..^suffix.Length].Trim('-', ' ');
                    changed = true;
                    break;
                }
            }
        }

        return clean;
    }

    private static bool IsBoldFontName(string name)
    {
        return name.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("-Bd", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("SemiBold", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Semibold", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Demi", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Heavy", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Black", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsItalicFontName(string name)
    {
        return name.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("-It", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Oblique", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateImportedFontFamily(string pdfName)
    {
        var cleaned = CleanPdfFontName(pdfName);
        var chars = cleaned
            .Select(static ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        var safe = new string(chars).Trim('_');
        return $"CanvasPdf_{(safe.Length == 0 ? "Font" : safe)}";
    }

    private static string CreateFontDataUri(ReadOnlyMemory<byte> bytes, string mimeType)
    {
        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes.ToArray())}";
    }

    private static string ImageBytesToDataUri(ReadOnlyMemory<byte> bytes)
    {
        var span = bytes.Span;
        string mime;
        if (span.Length >= 3 && span[0] == 0xFF && span[1] == 0xD8 && span[2] == 0xFF)
            mime = "image/jpeg";
        else if (span.Length >= 8
            && span[0] == 0x89 && span[1] == 0x50 && span[2] == 0x4E && span[3] == 0x47
            && span[4] == 0x0D && span[5] == 0x0A && span[6] == 0x1A && span[7] == 0x0A)
            mime = "image/png";
        else if (span.Length >= 6
            && span[0] == 0x47 && span[1] == 0x49 && span[2] == 0x46)
            mime = "image/gif";
        else
            mime = "image/png"; // assume properly-formed PNG for all else
        return $"data:{mime};base64,{Convert.ToBase64String(bytes.ToArray())}";
    }

    private sealed record CssFontInfo(
        string Family,
        string PdfName,
        bool Bold,
        bool Italic,
        string? DataUri = null,
        string? Format = null,
        string? DisplayName = null);
}
