using System.Globalization;
using System.Text;
using PXA.Core.Contracts;
using PXA.Importer;
using PXA.Importer.Analysis;
using PXA.Importer.Document;
using PXA.Importer.Graphics;

namespace PXA.FileImporter;

/// <summary>
/// Converts a PDF to a <see cref="DesignExportDto"/> using the PXA.Importer low-level
/// PDF engine. The engine tokenizes, parses, and interprets the raw PDF content streams
/// into a typed scene graph and Phase 5 primitive analysis layer which is then mapped
/// to PXA element DTOs.
/// </summary>
public sealed class PdfFileImporter : IFileImporter
{
    public IReadOnlyList<string> SupportedExtensions { get; } = ["pdf"];

    public Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        DoImportAsync(stream, name);

    private const double HeaderZone = 0.08;
    private const double FooterZone = 0.92;
    private const double ImportedTextLineHeight = 1.05;
    private const double ImportedTextHeightFactor = 1.18;
    private const double ImportedTextHorizontalBleed = 2d;

    // ── Entry point ───────────────────────────────────────────────────────────

    public static async Task<DesignExportDto> DoImportAsync(Stream stream, string? name = null)
    {
        var doc = await new PdfImporter().LoadAsync(stream);
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

            for (var index = 0; index < orderedPrimitives.Count; index++)
            {
                if (TryCollectSvgCluster(orderedPrimitives, index, out var cluster, out var consumed) &&
                    EmitSvgPathCluster(cluster, originX, pageH, pageNum, ref seq, elements))
                {
                    index += consumed - 1;
                    continue;
                }

                var primitive = orderedPrimitives[index];
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
                    !EmitSvgPath(path, originX, pageH, pg, ref seq, elements))
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

        var (x, y, w, h) = TextPxaFrame(el, originX, pageH);
        if (w < 1 || h < 1)
        {
            w = Math.Max(el.Text.Length * fs * 0.6, 20);
            h = fs * 1.5 + 4;
        }

        (x, y, w, h) = ExpandTextFrameForBrowserMetrics(x, y, w, h, fs);

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
            ["lineHeight"]  = ImportedTextLineHeight,
            ["whiteSpace"]  = "pre",
            ["rotation"]   = Math.Round(ToPxaRotation(el.Geometry.RotationDegrees), 2),
            ["pdfFontName"] = font.PdfName,
            ["pdfClassification"] = el.Classification.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(font.DataUri) && !string.IsNullOrWhiteSpace(font.Format))
        {
            style["fontDataUri"] = font.DataUri;
            style["fontFormat"] = font.Format;
            style["fontDisplayName"] = font.DisplayName ?? font.Family;
        }

        if (!string.IsNullOrWhiteSpace(font.EmbeddedFontSkippedReason))
        {
            style["pdfEmbeddedFontSkippedReason"] = font.EmbeddedFontSkippedReason;
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
        var paint = GetPathPaintIntent(el);
        bool hasFill   = paint.Fill;
        bool hasStroke = paint.Stroke && el.GraphicsState.LineWidth > 0;
        if (!hasFill && !hasStroke) return null;

        var (x, y, w, h) = ToPxaBounds(el.Bounds, originX, pageH);

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
                ["borderWidth"]     = hasStroke ? (int)Math.Max(0, Math.Round(el.GraphicsState.LineWidth)) : 0,
                ["borderStyle"]     = "solid",
                ["pdfClassification"] = el.Classification.ToString(),
            },
        };
    }

    private static ElementDto? MapImage(PrimitiveImage el, double originX, double pageH, int pg, ref int seq)
    {
        var (imgX, imgY, imgW, imgH, rotation) = TransformedPxaFrame(new PdfRectangle(0, 0, 1, 1), el.Transform, originX, pageH);

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
            FitMode = "contain",
            Style   = new Dictionary<string, object>
            {
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

        var paint = GetPathPaintIntent(el);
        bool hasFill   = paint.Fill;
        bool hasStroke = paint.Stroke && el.GraphicsState.LineWidth > 0;
        if (!hasFill && !hasStroke) return false;

        string fill   = hasFill   ? ColorToHex(el.GraphicsState.FillColor)   : "transparent";
        string stroke = hasStroke ? ColorToHex(el.GraphicsState.StrokeColor) : "transparent";

        foreach (var localRect in localRects)
        {
            // localRect is in path-user space; apply CTM to get global PDF coords
            var globalRect = MatrixEngine.TransformBounds(localRect, el.Transform);
            var (x, y, w, h) = ToPxaBounds(globalRect, originX, pageH);
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
                    ["borderWidth"]       = hasStroke ? (int)Math.Max(0, Math.Round(el.GraphicsState.LineWidth)) : 0,
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

    // ── Complex path → SVG data-URI image ────────────────────────────────────

    /// <summary>
    /// Emits a non-rectangular PrimitivePath as an SVG embedded in an image element.
    /// Path segment coordinates (pre-CTM) are mapped through the full CTM and Y-flipped into
    /// canvas space so the SVG viewBox can simply match the element's width×height.
    /// This preserves logo glyph outlines and other vector artwork that would otherwise be
    /// flattened to rectangular PXA shapes.
    /// </summary>
    private static bool EmitSvgPath(
        PrimitivePath el,
        double originX, double pageH,
        int pg, ref int seq,
        List<ElementDto> elements)
    {
        if (!IsComplexSvgPath(el)) return false;

        var paint = GetPathPaintIntent(el);
        bool hasFill   = paint.Fill;
        bool hasStroke = paint.Stroke && el.GraphicsState.LineWidth > 0;
        if (!hasFill && !hasStroke) return false;

        var (ex, ey, ew, eh) = ToPxaBounds(el.Bounds, originX, pageH);
        if (ew < 0.5 || eh < 0.5) return false;

        var d = BuildSvgPathData(el, originX, pageH, ex, ey);
        if (d.Length == 0) return false;

        string fill   = hasFill   ? ColorToHex(el.GraphicsState.FillColor)   : "none";
        string stroke = hasStroke ? ColorToHex(el.GraphicsState.StrokeColor) : "none";
        double sw     = hasStroke ? el.GraphicsState.LineWidth : 0;

        var fillRule = GetSvgFillRule(el);
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {SvgNum(ew)} {SvgNum(eh)}\" preserveAspectRatio=\"none\">" +
                  $"<path d=\"{d}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{SvgNum(sw)}\" fill-rule=\"{fillRule}\"/>" +
                  "</svg>";

        var dataUri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

        var rotation = ToPxaRotation(MatrixEngine.ExtractRotationDegrees(el.Transform));

        elements.Add(new ElementDto
        {
            Id      = $"svg-{pg}-{seq++}", Type = "image",
            X       = Math.Round(ex, 1),   Y    = Math.Round(ey, 1),
            Width   = Math.Round(ew, 1),   Height = Math.Round(eh, 1),
            Content = dataUri,
            FitMode = "fill",
            Style   = new Dictionary<string, object>
            {
                ["rotation"]          = Math.Round(rotation, 2),
                ["pdfClassification"] = el.Classification.ToString(),
                ["pdfVisualFallback"] = "svg-vector-path",
                ["pdfPrimitiveCount"] = 1,
                ["pdfVectorBounds"] = BoundsMetadata(el.Bounds),
            },
        });
        return true;
    }

    private static bool EmitSvgPathCluster(
        IReadOnlyList<PrimitivePath> paths,
        double originX, double pageH,
        int pg, ref int seq,
        List<ElementDto> elements)
    {
        if (paths.Count <= 1) return false;

        var bounds = paths[0].Bounds;
        for (var i = 1; i < paths.Count; i++)
        {
            bounds = bounds.Union(paths[i].Bounds);
        }

        var (ex, ey, ew, eh) = ToPxaBounds(bounds, originX, pageH);
        if (ew < 0.5 || eh < 0.5) return false;

        var sb = new StringBuilder();
        foreach (var path in paths)
        {
            var paint = GetPathPaintIntent(path);
            bool hasFill = paint.Fill;
            bool hasStroke = paint.Stroke && path.GraphicsState.LineWidth > 0;
            if (!hasFill && !hasStroke) continue;

            var d = BuildSvgPathData(path, originX, pageH, ex, ey);
            if (d.Length == 0) continue;

            string fill = hasFill ? ColorToHex(path.GraphicsState.FillColor) : "none";
            string stroke = hasStroke ? ColorToHex(path.GraphicsState.StrokeColor) : "none";
            double sw = hasStroke ? path.GraphicsState.LineWidth : 0;
            var fillRule = GetSvgFillRule(path);
            sb.Append($"<path d=\"{d}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{SvgNum(sw)}\" fill-rule=\"{fillRule}\"/>");
        }

        if (sb.Length == 0) return false;

        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {SvgNum(ew)} {SvgNum(eh)}\" preserveAspectRatio=\"none\">" +
                  sb +
                  "</svg>";

        var dataUri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

        elements.Add(new ElementDto
        {
            Id      = $"svggrp-{pg}-{seq++}",
            Type    = "image",
            X       = Math.Round(ex, 1),
            Y       = Math.Round(ey, 1),
            Width   = Math.Round(ew, 1),
            Height  = Math.Round(eh, 1),
            Content = dataUri,
            FitMode = "fill",
            Style = new Dictionary<string, object>
            {
                ["rotation"] = 0,
                ["pdfClassification"] = "VectorArtworkGroup",
                ["pdfPrimitiveCount"] = paths.Count,
                ["pdfVisualFallback"] = "svg-vector-cluster",
                ["pdfVectorBounds"] = BoundsMetadata(bounds),
            },
        });
        return true;
    }

    private static string BuildSvgPathData(
        PrimitivePath path,
        double originX,
        double pageH,
        double elementX,
        double elementY)
    {
        var sb = new StringBuilder();
        foreach (var seg in path.Segments)
        {
            switch (seg)
            {
                case MoveToSegment mt:
                    var (mx, my) = LocalToSvg(mt.Point, path.Transform, originX, pageH, elementX, elementY);
                    sb.Append($"M {SvgNum(mx)} {SvgNum(my)} ");
                    break;
                case LineToSegment lt:
                    var (lx, ly) = LocalToSvg(lt.Point, path.Transform, originX, pageH, elementX, elementY);
                    sb.Append($"L {SvgNum(lx)} {SvgNum(ly)} ");
                    break;
                case CurveToSegment ct:
                    var (c1x, c1y) = LocalToSvg(ct.Control1, path.Transform, originX, pageH, elementX, elementY);
                    var (c2x, c2y) = LocalToSvg(ct.Control2, path.Transform, originX, pageH, elementX, elementY);
                    var (cex, cey) = LocalToSvg(ct.End,      path.Transform, originX, pageH, elementX, elementY);
                    sb.Append($"C {SvgNum(c1x)} {SvgNum(c1y)} {SvgNum(c2x)} {SvgNum(c2y)} {SvgNum(cex)} {SvgNum(cey)} ");
                    break;
                case RectangleSegment rs:
                    var r = rs.Rectangle;
                    var (rx0, ry0) = LocalToSvg(new PdfPoint(r.Left,  r.Bottom), path.Transform, originX, pageH, elementX, elementY);
                    var (rx1, ry1) = LocalToSvg(new PdfPoint(r.Right, r.Bottom), path.Transform, originX, pageH, elementX, elementY);
                    var (rx2, ry2) = LocalToSvg(new PdfPoint(r.Right, r.Top),    path.Transform, originX, pageH, elementX, elementY);
                    var (rx3, ry3) = LocalToSvg(new PdfPoint(r.Left,  r.Top),    path.Transform, originX, pageH, elementX, elementY);
                    sb.Append($"M {SvgNum(rx0)} {SvgNum(ry0)} L {SvgNum(rx1)} {SvgNum(ry1)} L {SvgNum(rx2)} {SvgNum(ry2)} L {SvgNum(rx3)} {SvgNum(ry3)} Z ");
                    break;
                case ClosePathSegment:
                    sb.Append("Z ");
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    private static bool IsComplexSvgPath(PrimitivePath path)
    {
        if (path.Segments.Any(static segment => segment is CurveToSegment))
        {
            return true;
        }

        if (path.Segments.Count(segment => segment is MoveToSegment or LineToSegment) >= 3)
        {
            return true;
        }

        return path.Segments.Any(static segment => segment is ClosePathSegment);
    }

    private static bool TryCollectSvgCluster(
        IReadOnlyList<PrimitiveObject> primitives,
        int start,
        out IReadOnlyList<PrimitivePath> cluster,
        out int consumed)
    {
        cluster = [];
        consumed = 0;

        if (start >= primitives.Count ||
            primitives[start] is not PrimitivePath first ||
            !CanClusterSvgPath(first))
        {
            return false;
        }

        var paths = new List<PrimitivePath> { first };
        var clusterBounds = first.Bounds;
        consumed = 1;

        for (var index = start + 1; index < primitives.Count; index++)
        {
            if (primitives[index] is not PrimitivePath next || !CanClusterSvgPath(next))
            {
                break;
            }

            if (!CanJoinSvgCluster(clusterBounds, next.Bounds) ||
                !HasCompatiblePaint(first, next))
            {
                break;
            }

            paths.Add(next);
            clusterBounds = clusterBounds.Union(next.Bounds);
            consumed++;
        }

        if (paths.Count < 2)
        {
            cluster = [];
            consumed = 0;
            return false;
        }

        cluster = paths;
        return true;
    }

    private static bool CanClusterSvgPath(PrimitivePath path)
    {
        if (!IsComplexSvgPath(path)) return false;

        var bounds = path.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        // Logos and glyph outlines are usually small/medium vector fragments. Very large
        // artwork stays as individual SVGs to avoid accidentally merging page decorations.
        return bounds.Width <= 240d && bounds.Height <= 120d;
    }

    private static bool CanJoinSvgCluster(PdfRectangle cluster, PdfRectangle next)
    {
        var verticalOverlap = Math.Min(cluster.Top, next.Top) - Math.Max(cluster.Bottom, next.Bottom);
        var minHeight = Math.Max(1d, Math.Min(cluster.Height, next.Height));
        if (verticalOverlap < minHeight * 0.15d)
        {
            return false;
        }

        var horizontalGap = next.Left > cluster.Right
            ? next.Left - cluster.Right
            : cluster.Left > next.Right
                ? cluster.Left - next.Right
                : 0d;

        return horizontalGap <= Math.Max(12d, Math.Max(cluster.Height, next.Height) * 1.75d);
    }

    private static bool HasCompatiblePaint(PrimitivePath left, PrimitivePath right)
    {
        var leftPaint = GetPathPaintIntent(left);
        var rightPaint = GetPathPaintIntent(right);
        return leftPaint == rightPaint &&
            left.GraphicsState.FillColor == right.GraphicsState.FillColor &&
            left.GraphicsState.StrokeColor == right.GraphicsState.StrokeColor &&
            Math.Abs(left.GraphicsState.LineWidth - right.GraphicsState.LineWidth) < 0.001d;
    }

    private static string SvgNum(double value)
    {
        var normalized = Math.Abs(value) < 0.00005d ? 0d : value;
        return normalized.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, object> BoundsMetadata(PdfRectangle bounds)
    {
        return new Dictionary<string, object>
        {
            ["x"] = Math.Round(bounds.X, 4),
            ["y"] = Math.Round(bounds.Y, 4),
            ["width"] = Math.Round(bounds.Width, 4),
            ["height"] = Math.Round(bounds.Height, 4),
        };
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

    private static (double x, double y, double width, double height) ToPxaBounds(
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

    private static (double x, double y, double width, double height) TextPxaFrame(
        PrimitiveText text,
        double originX,
        double pageH)
    {
        var fontSize = Math.Max(text.FontSize, 1d);
        var localWidth = Math.Max(fontSize * 0.35d, text.Text.Length * fontSize * 0.5d);
        var localBounds = new PdfRectangle(0, -fontSize * 0.2d, localWidth, fontSize);
        var (x, y, width, height, _) = TransformedPxaFrame(localBounds, text.Transform, originX, pageH);
        return (x, y, width, height);
    }

    private static (double x, double y, double width, double height) ExpandTextFrameForBrowserMetrics(
        double x,
        double y,
        double width,
        double height,
        double fontSize)
    {
        var targetWidth = Math.Max(width + ImportedTextHorizontalBleed, width * 1.04d);
        var targetHeight = Math.Max(height, fontSize * ImportedTextHeightFactor);

        x -= (targetWidth - width) / 2d;
        y -= (targetHeight - height) / 2d;

        return (x, y, targetWidth, targetHeight);
    }

    private static (double x, double y, double width, double height, double rotation) TransformedPxaFrame(
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
            ToPxaRotation(MatrixEngine.ExtractRotationDegrees(transform)));
    }

    private static double ToPxaRotation(double pdfDegrees)
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

    private static PathPaintIntent GetPathPaintIntent(PrimitiveObject primitive)
    {
        return primitive.SourceOperator.Operator.Name switch
        {
            "S" or "s" => new PathPaintIntent(Fill: false, Stroke: true),
            "f" or "F" or "f*" => new PathPaintIntent(Fill: true, Stroke: false),
            "B" or "B*" or "b" or "b*" => new PathPaintIntent(Fill: true, Stroke: true),
            _ => new PathPaintIntent(
                Fill: primitive.GraphicsState.FillColor != default,
                Stroke: primitive.GraphicsState.StrokeColor != default)
        };
    }

    private static string GetSvgFillRule(PrimitiveObject primitive)
    {
        return primitive.SourceOperator.Operator.Name.EndsWith('*') ? "evenodd" : "nonzero";
    }

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

        string? skippedReason = null;
        if (text.IsSubsetFont && !text.EmbeddedFontBytes.IsEmpty)
        {
            skippedReason = "Embedded PDF subset font is unsafe for browser Unicode text rendering.";
        }
        else if (text.UsesToUnicodeMap && !text.EmbeddedFontBytes.IsEmpty)
        {
            skippedReason = "ToUnicode remaps PDF glyph codes to Unicode; embedded subset font is unsafe for browser text rendering.";
        }

        if (!text.IsSubsetFont &&
            !text.UsesToUnicodeMap &&
            !text.EmbeddedFontBytes.IsEmpty &&
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

        return new CssFontInfo(family, pdfName, bold, italic, dataUri, format, displayName, skippedReason);
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
        return $"PxaPdf_{(safe.Length == 0 ? "Font" : safe)}";
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
        string? DisplayName = null,
        string? EmbeddedFontSkippedReason = null);

    private readonly record struct PathPaintIntent(bool Fill, bool Stroke);
}
