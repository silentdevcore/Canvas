using Canvas.Core.Contracts;
using SkiaSharp;
using System.Globalization;

namespace Canvas.FileImporter.ImageAnalysis.Analysis;

/// <summary>
/// Phase 5: combines the outputs of Phases 2–4 into an ordered list of
/// <see cref="ImagePrimitive"/> objects, deduplicates overlapping elements,
/// and maps them to <see cref="DesignExportDto"/>.
/// </summary>
public static class SceneAssembler
{
    // ── Assembly ──────────────────────────────────────────────────────────────

    public static IReadOnlyList<ImagePrimitive> Assemble(
        ColorAnalysisResult  colors,
        ShapeDetectionResult shapes,
        TextAnalysisResult   texts)
    {
        var all = new List<ImagePrimitive>();
        int z   = 0;

        // Pre-build text regions. Shapes tightly inside these are usually
        // character-stroke artifacts, while larger panels/long rules must survive.
        var textRegions = texts.Lines
            .Select(l => l.Bounds)
            .ToList();

        // Dark colour regions (e.g. navy headers) often contain undetected white/coloured text.
        // Suppress shape artefacts inside them too.
        var darkRegions = colors.Regions
            .Where(r => (r.FillColor.Red * 77 + r.FillColor.Green * 150 + r.FillColor.Blue * 29) >> 8 < 80)
            .Select(r => r.Bounds)
            .ToList();
        var nonTextClusterRegions = shapes.Shapes
            .Where(s => s.AnalysisType is "icon-cluster" or "image-cluster")
            .Select(s => s.Bounds)
            .ToList();
        var gridLineMetadata = DetectGridLineMetadata(shapes.Shapes);

        // Z-order: colour regions (bottom) → shapes → text (top)
        foreach (var region in colors.Regions)
        {
            all.Add(new ImageRegionPrimitive
            {
                Bounds    = region.Bounds,
                FillColor = region.FillColor,
                Coverage  = region.Coverage,
                AnalysisType = region.AnalysisType,
                Confidence = region.Confidence,
                SourceKind = region.SourceKind,
                ZOrder    = z++,
            });
        }

        foreach (var shape in shapes.Shapes)
        {
            // Suppress shapes/lines inside text regions or dark colour regions
            // (character-stroke artefacts from undetected white/coloured-on-dark text).
            if (IsTextStrokeArtifact(shape, textRegions) ||
                IsDarkPanelTextStrokeArtifact(shape, darkRegions)) continue;

            if (IsDuplicateOfExistingRegion(all, shape)) continue;

            // Suppress colour regions fully covered by a matching-fill shape
            RemoveCoveredRegions(all, shape.Bounds);

            gridLineMetadata.TryGetValue(shape.Bounds, out var gridMetadata);
            all.Add(new ImageShapePrimitive
            {
                Bounds      = shape.Bounds,
                Kind        = shape.Kind,
                FillColor   = shape.FillColor,
                StrokeColor = shape.StrokeColor,
                StrokeWidth = shape.StrokeWidth,
                Confidence  = shape.Confidence,
                AnalysisType = gridMetadata is not null ? "grid-line" : shape.AnalysisType,
                GridId = gridMetadata?.GridId,
                GridOrientation = gridMetadata?.Orientation,
                GridBounds = gridMetadata?.Bounds,
                CornerRadiusPx = shape.CornerRadiusPx,
                ZOrder      = z++,
            });
        }

        foreach (var line in AssignTextBlocks(texts.Lines))
        {
            if (IsWeakTextInsideNonTextCluster(line, nonTextClusterRegions))
                continue;

            all.Add(new ImageTextPrimitive
            {
                Bounds     = line.Bounds,
                Words      = line.Words,
                FontSizePx = line.FontSizePx,
                BaselineY  = line.BaselineY,
                TextBlockId = line.TextBlockId,
                TextBlockLineIndex = line.TextBlockLineIndex,
                TextColor  = line.TextColor,
                ZOrder     = z++,
            });
        }

        return all.OrderBy(p => p.ZOrder).ToList();
    }

    private static void RemoveCoveredRegions(List<ImagePrimitive> primitives, SKRectI shapeBounds)
    {
        primitives.RemoveAll(p =>
            p is ImageRegionPrimitive r &&
            r.AnalysisType != "image-region" &&
            IsMostlyCovered(r.Bounds, shapeBounds));
    }

    private static bool IsDuplicateOfExistingRegion(List<ImagePrimitive> primitives, ImageShapePrimitive shape)
    {
        if (shape.Kind != ShapeKind.Rect) return false;
        if (shape.AnalysisType == "rounded-rect") return false;
        if (shape.FillColor == SKColors.Transparent) return false;

        return primitives.Any(p =>
            p is ImageRegionPrimitive region &&
            region.AnalysisType != "image-region" &&
            IsMostlyCovered(region.Bounds, shape.Bounds) &&
            IsMostlyCovered(shape.Bounds, region.Bounds) &&
            ColorAnalyzer.ColorDistance(region.FillColor, shape.FillColor) <= ColorAnalyzer.ColorTolerance);
    }

    private static bool CanSuppressInsideDarkRegion(ImageShapePrimitive shape) =>
        shape.AnalysisType is not ("rounded-rect" or "icon-cluster" or "image-cluster" or "grid-line");

    private static bool IsMostlyCovered(SKRectI regionBounds, SKRectI coveringBounds)
    {
        double regionArea = (double)regionBounds.Width * regionBounds.Height;
        if (regionArea <= 0) return false;

        int ox = Math.Max(0, Math.Min(regionBounds.Right, coveringBounds.Right) -
                             Math.Max(regionBounds.Left, coveringBounds.Left));
        int oy = Math.Max(0, Math.Min(regionBounds.Bottom, coveringBounds.Bottom) -
                             Math.Max(regionBounds.Top, coveringBounds.Top));

        return (ox * oy) / regionArea >= 0.95;
    }

    private static bool OverlapsTextRegion(SKRectI shapeBounds, List<SKRectI> textRegions)
    {
        double shapeArea = (double)shapeBounds.Width * shapeBounds.Height;
        if (shapeArea <= 0) return false;
        foreach (var tr in textRegions)
        {
            int ox = Math.Max(0, Math.Min(shapeBounds.Right,  tr.Right)  - Math.Max(shapeBounds.Left, tr.Left));
            int oy = Math.Max(0, Math.Min(shapeBounds.Bottom, tr.Bottom) - Math.Max(shapeBounds.Top,  tr.Top));
            if ((double)(ox * oy) / shapeArea > 0.5) return true;
        }
        return false;
    }

    private static bool IsTextStrokeArtifact(ImageShapePrimitive shape, List<SKRectI> textRegions)
    {
        double shapeArea = (double)shape.Bounds.Width * shape.Bounds.Height;
        if (shapeArea <= 0) return false;

        foreach (var textRegion in textRegions)
        {
            var overlap = Overlap(shape.Bounds, textRegion);
            if (overlap.Area <= 0) continue;

            double shapeOverlap = overlap.Area / shapeArea;
            double textArea = Math.Max(1, textRegion.Width * textRegion.Height);
            double shapeToTextArea = shapeArea / textArea;

            if (shapeOverlap >= 0.85 && shapeToTextArea <= 0.35)
                return true;

            bool thinInsideText =
                shape.Kind == ShapeKind.Line &&
                shapeOverlap >= 0.65 &&
                shape.Bounds.Width <= textRegion.Width * 0.75 &&
                shape.Bounds.Height <= Math.Max(6, textRegion.Height * 0.35);
            if (thinInsideText)
                return true;
        }

        return false;
    }

    private static bool IsDarkPanelTextStrokeArtifact(ImageShapePrimitive shape, List<SKRectI> darkRegions)
    {
        if (!CanSuppressInsideDarkRegion(shape))
            return false;

        double shapeArea = (double)shape.Bounds.Width * shape.Bounds.Height;
        if (shapeArea <= 0)
            return false;

        foreach (var darkRegion in darkRegions)
        {
            var overlap = Overlap(shape.Bounds, darkRegion);
            if (overlap.Area / shapeArea <= 0.70)
                continue;

            double darkArea = Math.Max(1, darkRegion.Width * darkRegion.Height);
            double shapeToDarkArea = shapeArea / darkArea;
            bool smallGlyphSized = shape.Bounds.Width <= 72 &&
                                   shape.Bounds.Height <= 36 &&
                                   shapeToDarkArea <= 0.08;
            bool thinStrokeSized = shape.Kind == ShapeKind.Line &&
                                   shape.Bounds.Width <= 96 &&
                                   shape.Bounds.Height <= 12 &&
                                   shapeToDarkArea <= 0.06;

            if (smallGlyphSized || thinStrokeSized)
                return true;
        }

        return false;
    }

    private static bool IsWeakTextInsideNonTextCluster(ImageTextPrimitive line, List<SKRectI> clusterRegions)
    {
        if (clusterRegions.Count == 0)
            return false;

        double confidence = AverageTextConfidence(line);
        if (confidence >= 0.50 && !line.Text.Contains('?'))
            return false;

        double lineArea = Math.Max(1, line.Bounds.Width * line.Bounds.Height);
        foreach (var cluster in clusterRegions)
        {
            var overlap = Overlap(line.Bounds, cluster);
            if (overlap.Area / lineArea >= 0.65)
                return true;
        }

        return false;
    }

    private static (int Width, int Height, int Area) Overlap(SKRectI a, SKRectI b)
    {
        int width = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
        int height = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
        return (width, height, width * height);
    }

    private static SKRectI UnionBounds(IEnumerable<SKRectI> bounds)
    {
        var list = bounds.ToList();
        if (list.Count == 0)
            return SKRectI.Empty;

        return new SKRectI(
            list.Min(b => b.Left),
            list.Min(b => b.Top),
            list.Max(b => b.Right),
            list.Max(b => b.Bottom));
    }

    private sealed record GridLineMetadata(int GridId, string Orientation, SKRectI Bounds);

    private static Dictionary<SKRectI, GridLineMetadata> DetectGridLineMetadata(IReadOnlyList<ImageShapePrimitive> shapes)
    {
        var horizontal = shapes
            .Where(s => s.Kind == ShapeKind.Line && s.Bounds.Width >= s.Bounds.Height * 3)
            .ToList();
        var vertical = shapes
            .Where(s => s.Kind == ShapeKind.Line && s.Bounds.Height >= s.Bounds.Width * 3)
            .ToList();

        var gridLines = new HashSet<SKRectI>();
        foreach (var h in horizontal)
        {
            int intersections = vertical.Count(v => LinesIntersect(h.Bounds, v.Bounds));
            if (intersections >= 2)
                gridLines.Add(h.Bounds);
        }

        foreach (var v in vertical)
        {
            int intersections = horizontal.Count(h => LinesIntersect(h.Bounds, v.Bounds));
            if (intersections >= 2)
                gridLines.Add(v.Bounds);
        }

        if (gridLines.Count == 0)
            return [];

        var gridBounds = UnionBounds(gridLines);
        return gridLines.ToDictionary(
            bounds => bounds,
            bounds => new GridLineMetadata(
                GridId: 1,
                Orientation: bounds.Width >= bounds.Height ? "horizontal" : "vertical",
                Bounds: gridBounds));
    }

    private static bool LinesIntersect(SKRectI a, SKRectI b)
    {
        var overlap = Overlap(ExpandForLineIntersection(a), ExpandForLineIntersection(b));
        return overlap.Area > 0;
    }

    private static SKRectI ExpandForLineIntersection(SKRectI rect)
    {
        const int tolerance = 3;
        return new SKRectI(
            rect.Left - tolerance,
            rect.Top - tolerance,
            rect.Right + tolerance,
            rect.Bottom + tolerance);
    }

    private static IReadOnlyList<ImageTextPrimitive> AssignTextBlocks(IReadOnlyList<ImageTextPrimitive> lines)
    {
        if (lines.Count == 0) return [];

        var sorted = lines
            .OrderBy(l => l.Bounds.Top)
            .ThenBy(l => l.Bounds.Left)
            .ToList();
        var assigned = new List<ImageTextPrimitive>(sorted.Count);
        var blockLastLine = new Dictionary<int, ImageTextPrimitive>();
        var blockBounds = new Dictionary<int, SKRectI>();
        var blockLineCounts = new Dictionary<int, int>();
        int nextBlockId = 1;

        foreach (var line in sorted)
        {
            int? blockId = FindTextBlock(line, blockLastLine, blockBounds);
            if (blockId is null)
            {
                blockId = nextBlockId++;
                blockLineCounts[blockId.Value] = 0;
                blockBounds[blockId.Value] = line.Bounds;
            }

            int lineIndex = blockLineCounts[blockId.Value]++;
            var copy = CopyTextLine(line, blockId.Value, lineIndex);
            assigned.Add(copy);
            blockLastLine[blockId.Value] = copy;
            blockBounds[blockId.Value] = Union(blockBounds[blockId.Value], copy.Bounds);
        }

        var blockOrder = assigned
            .GroupBy(l => l.TextBlockId ?? 0)
            .Select(g => new
            {
                BlockId = g.Key,
                Left = g.Min(l => l.Bounds.Left),
                Top = g.Min(l => l.Bounds.Top),
            })
            .OrderBy(g => g.Left)
            .ThenBy(g => g.Top)
            .Select((g, index) => new { g.BlockId, Order = index })
            .ToDictionary(g => g.BlockId, g => g.Order);

        return assigned
            .OrderBy(l => blockOrder[l.TextBlockId ?? 0])
            .ThenBy(l => l.TextBlockLineIndex)
            .ThenBy(l => l.Bounds.Top)
            .ThenBy(l => l.Bounds.Left)
            .ToList();
    }

    private static int? FindTextBlock(
        ImageTextPrimitive line,
        Dictionary<int, ImageTextPrimitive> blockLastLine,
        Dictionary<int, SKRectI> blockBounds)
    {
        int? bestId = null;
        double bestGap = double.MaxValue;

        foreach (var (blockId, previous) in blockLastLine)
        {
            double verticalGap = line.Bounds.Top - previous.Bounds.Bottom;
            if (verticalGap < -Math.Max(line.FontSizePx, previous.FontSizePx) * 0.5)
                continue;

            double maxGap = Math.Max(line.FontSizePx, previous.FontSizePx) * 1.8;
            if (verticalGap > maxGap)
                continue;

            if (!LooksLikeSameTextBlock(previous, blockBounds[blockId], line))
                continue;

            if (verticalGap < bestGap)
            {
                bestGap = verticalGap;
                bestId = blockId;
            }
        }

        return bestId;
    }

    private static bool LooksLikeSameTextBlock(ImageTextPrimitive previous, SKRectI blockBounds, ImageTextPrimitive current)
    {
        int lineOverlap = HorizontalOverlap(previous.Bounds, current.Bounds);
        int blockOverlap = HorizontalOverlap(blockBounds, current.Bounds);
        int minLineWidth = Math.Max(1, Math.Min(previous.Bounds.Width, current.Bounds.Width));
        int minBlockWidth = Math.Max(1, Math.Min(blockBounds.Width, current.Bounds.Width));
        double lineOverlapRatio = (double)lineOverlap / minLineWidth;
        double blockOverlapRatio = (double)blockOverlap / minBlockWidth;
        double leftDelta = Math.Abs(previous.Bounds.Left - current.Bounds.Left);
        double blockLeftDelta = Math.Abs(blockBounds.Left - current.Bounds.Left);
        double fontDelta = Math.Abs(previous.FontSizePx - current.FontSizePx);
        double tolerance = Math.Max(previous.FontSizePx, current.FontSizePx) * 2.5;
        double paragraphIndentTolerance = Math.Max(previous.FontSizePx, current.FontSizePx) * 4.0;
        bool similarLineHeight = fontDelta <= Math.Max(previous.FontSizePx, current.FontSizePx) * 0.45;

        return similarLineHeight &&
               (lineOverlapRatio >= 0.25 ||
                blockOverlapRatio >= 0.35 ||
                leftDelta <= tolerance ||
                blockLeftDelta <= paragraphIndentTolerance);
    }

    private static int HorizontalOverlap(SKRectI a, SKRectI b) =>
        Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));

    private static SKRectI Union(SKRectI a, SKRectI b) => new(
        Math.Min(a.Left, b.Left),
        Math.Min(a.Top, b.Top),
        Math.Max(a.Right, b.Right),
        Math.Max(a.Bottom, b.Bottom));

    private static ImageTextPrimitive CopyTextLine(
        ImageTextPrimitive line,
        int textBlockId,
        int lineIndex) => new()
    {
        Bounds = line.Bounds,
        Words = line.Words,
        FontSizePx = line.FontSizePx,
        BaselineY = line.BaselineY,
        TextBlockId = textBlockId,
        TextBlockLineIndex = lineIndex,
        TextColor = line.TextColor,
        ZOrder = line.ZOrder,
    };

    // ── DTO mapping ───────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a list of assembled primitives into a <see cref="DesignExportDto"/>.
    /// When <paramref name="targetWidthPt"/> and <paramref name="targetHeightPt"/> are
    /// provided the output is scaled uniformly to fit the requested page size (e.g. A4).
    /// </summary>
    public static DesignExportDto ToDesign(
        IReadOnlyList<ImagePrimitive> primitives,
        SKColor background,
        int imageWidthPx,
        int imageHeightPx,
        double scaleFactor,
        string name,
        double? targetWidthPt  = null,
        double? targetHeightPt = null,
        ImageAnalysisOptions? options = null,
        string? fallbackImageDataUri = null)
    {
        options ??= ImageAnalysisOptions.Default;

        // Source page dimensions in points (after preprocessing downscale)
        double srcW = imageWidthPx  / scaleFactor;
        double srcH = imageHeightPx / scaleFactor;

        double? dpiX = NormalizeDpi(options.SourceDpiX);
        double? dpiY = NormalizeDpi(options.SourceDpiY);
        double sourcePointScaleX = dpiX is > 0 ? 72.0 / dpiX.Value : 1.0;
        double sourcePointScaleY = dpiY is > 0 ? 72.0 / dpiY.Value : sourcePointScaleX;

        double pageW, pageH, s;
        if (targetWidthPt.HasValue && targetHeightPt.HasValue)
        {
            pageW = targetWidthPt.Value;
            pageH = targetHeightPt.Value;
            // Uniform fit: scale so the image fills the target page without stretching
            s = Math.Min(pageW / srcW, pageH / srcH) / scaleFactor;
        }
        else
        {
            pageW = srcW * sourcePointScaleX;
            pageH = srcH * sourcePointScaleY;
            s     = Math.Min(sourcePointScaleX, sourcePointScaleY) / scaleFactor;
        }

        var elements = new List<ElementDto>();
        int seq      = 0;

        if (options.IncludeFallbackImageLayer && fallbackImageDataUri is not null)
        {
            elements.Add(BuildFallbackImageElement(fallbackImageDataUri, pageW, pageH, srcW, srcH, ref seq));
        }

        foreach (var primitive in primitives)
        {
            var dto = MapPrimitive(primitive, s, options.LowConfidenceThreshold, ref seq);
            if (dto is not null) elements.Add(dto);
        }

        return new DesignExportDto
        {
            Id             = Guid.NewGuid().ToString("N")[..12],
            Name           = name,
            Pages          = [new PageDto { Id = "page-1", Elements = elements }],
            SharedElements = [],
            PageSettings   = new PageSettingsDto
            {
                Width       = Math.Round(pageW, 1),
                Height      = Math.Round(pageH, 1),
                Orientation = pageW > pageH ? "landscape" : "portrait",
                Unit       = "pt",
                BackgroundColor = ColorToHex(background),
                Margins     = new MarginsDto { Top = 0, Right = 0, Bottom = 0, Left = 0 },
                CustomProperties = BuildImageAnalysisPageProperties(
                    imageWidthPx,
                    imageHeightPx,
                    scaleFactor,
                    srcW,
                    srcH,
                    dpiX,
                    dpiY),
            },
        };
    }

    private static double? NormalizeDpi(double? dpi) =>
        dpi is > 0 and <= 2400 ? dpi : null;

    private static List<CustomDocumentPropertyDto> BuildImageAnalysisPageProperties(
        int workingWidthPx,
        int workingHeightPx,
        double scaleFactor,
        double sourceWidthPx,
        double sourceHeightPx,
        double? dpiX,
        double? dpiY)
    {
        var properties = new List<CustomDocumentPropertyDto>
        {
            NumberProperty("imageAnalysis.sourceWidthPx", sourceWidthPx),
            NumberProperty("imageAnalysis.sourceHeightPx", sourceHeightPx),
            NumberProperty("imageAnalysis.workingWidthPx", workingWidthPx),
            NumberProperty("imageAnalysis.workingHeightPx", workingHeightPx),
            NumberProperty("imageAnalysis.scaleFactor", scaleFactor),
            new()
            {
                Name = "imageAnalysis.pageScaleSource",
                Value = dpiX is > 0 || dpiY is > 0 ? "explicit-dpi" : "pixel-points",
                Type = "text",
            },
        };

        if (dpiX is > 0)
            properties.Add(NumberProperty("imageAnalysis.sourceDpiX", dpiX.Value));
        if (dpiY is > 0)
            properties.Add(NumberProperty("imageAnalysis.sourceDpiY", dpiY.Value));

        return properties;
    }

    private static CustomDocumentPropertyDto NumberProperty(string name, double value) => new()
    {
        Name = name,
        Value = Math.Round(value, 4).ToString(CultureInfo.InvariantCulture),
        Type = "number",
    };

    private static ElementDto? MapPrimitive(
        ImagePrimitive primitive,
        double s,
        double lowConfidenceThreshold,
        ref int seq)
    {
        var (x, y, w, h) = ToPageCoords(primitive.Bounds, s);
        if (w < 0.5 || h < 0.5) return null;

        return primitive switch
        {
            ImageRegionPrimitive region => new ElementDto
            {
                Id     = $"region-{seq++}", Type = "shape",
                X      = x, Y = y, Width = w, Height = h,
                Style  = WithAnalysisMetadata(
                    new Dictionary<string, object>
                    {
                        ["backgroundColor"] = ColorToHex(region.FillColor),
                        ["borderWidth"]     = 0,
                        ["imageAnalysisType"] = region.AnalysisType ?? "color-region",
                        ["imageAnalysisSource"] = region.SourceKind ?? "color-region",
                    },
                    region.Bounds,
                    confidence: region.Confidence,
                    lowConfidenceThreshold),
            },

            ImageShapePrimitive shape when shape.Kind == ShapeKind.Line => new ElementDto
            {
                Id     = $"line-{seq++}", Type = "rect",
                X      = x, Y = y, Width = Math.Max(0.5, w), Height = Math.Max(0.5, h),
                Style  = WithShapeSemanticMetadata(
                    shape,
                    WithAnalysisMetadata(
                        new Dictionary<string, object>
                        {
                            ["backgroundColor"] = ColorToHex(shape.StrokeColor),
                            ["borderWidth"]     = 0,
                            ["imageAnalysisType"] = shape.AnalysisType ?? "line",
                        },
                        shape.Bounds,
                        shape.Confidence,
                        lowConfidenceThreshold)),
            },

            ImageShapePrimitive shape => new ElementDto
            {
                Id     = $"shape-{seq++}", Type = "shape",
                X      = x, Y = y, Width = w, Height = h,
                Style  = BuildShapeStyle(shape, lowConfidenceThreshold),
            },

            ImageTextPrimitive text => BuildTextElement(text, x, y, w, h, s, lowConfidenceThreshold, seq++),

            _ => null,
        };
    }

    private static Dictionary<string, object> WithShapeSemanticMetadata(
        ImageShapePrimitive shape,
        Dictionary<string, object> style)
    {
        if (shape.GridId is not null)
        {
            style["imageAnalysisGridId"] = shape.GridId.Value;
            style["imageAnalysisGridOrientation"] = shape.GridOrientation ?? "";
            if (shape.GridBounds is SKRectI gridBounds)
            {
                style["imageAnalysisGridBoundsPx"] = new Dictionary<string, object>
                {
                    ["x"] = gridBounds.Left,
                    ["y"] = gridBounds.Top,
                    ["width"] = gridBounds.Width,
                    ["height"] = gridBounds.Height,
                };
            }
        }

        return style;
    }

    private static ElementDto BuildFallbackImageElement(
        string dataUri,
        double pageW,
        double pageH,
        double sourceW,
        double sourceH,
        ref int seq)
    {
        return new ElementDto
        {
            Id = $"fallback-image-{seq++}",
            Type = "image",
            X = 0,
            Y = 0,
            Width = Math.Round(pageW, 1),
            Height = Math.Round(pageH, 1),
            Content = dataUri,
            FitMode = "fill",
            Locked = true,
            Style = new Dictionary<string, object>
            {
                ["imageAnalysisType"] = "fallback-image",
                ["imageAnalysisConfidence"] = 1.0,
                ["sourceBoundsPx"] = new Dictionary<string, object>
                {
                    ["x"] = 0,
                    ["y"] = 0,
                    ["width"] = Math.Round(sourceW, 1),
                    ["height"] = Math.Round(sourceH, 1),
                },
            },
        };
    }

    private static Dictionary<string, object> BuildShapeStyle(
        ImageShapePrimitive shape,
        double lowConfidenceThreshold)
    {
        var style = WithShapeSemanticMetadata(
            shape,
            WithAnalysisMetadata(new Dictionary<string, object>
            {
                ["backgroundColor"]   = shape.FillColor == SKColors.Transparent
                    ? "transparent" : ColorToHex(shape.FillColor),
                ["borderColor"]       = shape.StrokeColor == SKColors.Transparent
                    ? "transparent" : ColorToHex(shape.StrokeColor),
                ["borderWidth"]       = shape.StrokeWidth,
                ["borderStyle"]       = "solid",
                ["imageAnalysisType"] = shape.AnalysisType ?? shape.Kind.ToString().ToLowerInvariant(),
            }, shape.Bounds, shape.Confidence, lowConfidenceThreshold));
        if (shape.Kind == ShapeKind.Ellipse)
            style["borderRadius"] = "50%";
        else if (shape.CornerRadiusPx is double radius && radius > 0)
            style["borderRadius"] = Math.Round(radius, 1);
        return style;
    }

    private static ElementDto BuildTextElement(
        ImageTextPrimitive text,
        double x, double y, double w, double h,
        double s, double lowConfidenceThreshold, int seqNum)
    {
        string content = text.Text;
        if (string.IsNullOrWhiteSpace(content))
            content = string.Concat(text.Words.Select(wd => wd.Text + " ")).Trim();

        double fs = Math.Max(6, text.FontSizePx * s);
        double confidence = AverageTextConfidence(text);

        return new ElementDto
        {
            Id      = $"text-{seqNum}",
            Type    = "text",
            X       = x, Y = y, Width = w, Height = Math.Max(h, fs * 1.3),
            Content = content,
            Style   = WithAnalysisMetadata(
                new Dictionary<string, object>
                {
                    ["fontSize"]          = Math.Round(fs, 1),
                    ["fontFamily"]        = "Arial",
                    ["color"]             = ColorToHex(text.TextColor),
                    ["fontWeight"]        = fs >= 16.0 ? "bold" : "normal",
                    ["imageAnalysisType"] = "text",
                    ["baselineYPx"]       = Math.Round(text.BaselineY, 1),
                    ["textBlockId"]       = text.TextBlockId ?? 0,
                    ["textBlockLineIndex"] = text.TextBlockLineIndex,
                    ["imageAnalysisGlyphs"] = BuildGlyphDiagnostics(text),
                },
                text.Bounds,
                confidence,
                lowConfidenceThreshold),
        };
    }
    private static double AverageTextConfidence(ImageTextPrimitive text)
    {
        var chars = text.Words.SelectMany(w => w.Chars).ToList();
        if (chars.Count == 0) return 0;
        return chars.Average(c => c.Confidence);
    }

    private static IReadOnlyList<Dictionary<string, object>> BuildGlyphDiagnostics(ImageTextPrimitive text)
    {
        return text.Words
            .SelectMany(w => w.Chars)
            .Select(c =>
            {
                var item = new Dictionary<string, object>
                {
                    ["value"] = c.Value.ToString(),
                    ["confidence"] = Math.Round(Math.Clamp(c.Confidence, 0, 1), 3),
                    ["boundsPx"] = new Dictionary<string, object>
                    {
                        ["x"] = c.Bounds.Left,
                        ["y"] = c.Bounds.Top,
                        ["width"] = c.Bounds.Width,
                        ["height"] = c.Bounds.Height,
                    },
                };

                if (c.Diagnostics is not null)
                {
                    item["initialCandidate"] = c.Diagnostics.InitialCandidate.ToString();
                    item["selectedCandidate"] = c.Diagnostics.SelectedCandidate.ToString();
                    item["method"] = c.Diagnostics.Method;
                    item["score"] = c.Diagnostics.Score;
                    item["enclosedWhiteRegions"] = c.Diagnostics.EnclosedWhiteRegions;
                    item["projectionReranked"] = c.Diagnostics.ProjectionReranked;
                    item["zoningReranked"] = c.Diagnostics.ZoningReranked;
                    item["signals"] = c.Diagnostics.Signals.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                    item["decisionWeights"] = c.Diagnostics.DecisionWeights.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                }

                return item;
            })
            .ToList();
    }

    private static Dictionary<string, object> WithAnalysisMetadata(
        Dictionary<string, object> style,
        SKRectI sourceBounds,
        double confidence,
        double lowConfidenceThreshold)
    {
        double normalizedConfidence = Math.Clamp(confidence, 0, 1);
        style["imageAnalysisConfidence"] = Math.Round(normalizedConfidence, 3);
        if (normalizedConfidence < lowConfidenceThreshold)
            style["imageAnalysisLowConfidence"] = true;
        style["sourceBoundsPx"] = new Dictionary<string, object>
        {
            ["x"] = sourceBounds.Left,
            ["y"] = sourceBounds.Top,
            ["width"] = sourceBounds.Width,
            ["height"] = sourceBounds.Height,
        };
        return style;
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private static (double x, double y, double w, double h) ToPageCoords(SKRectI bounds, double s)
    {
        return (
            Math.Round(bounds.Left   * s, 1),
            Math.Round(bounds.Top    * s, 1),
            Math.Round(bounds.Width  * s, 1),
            Math.Round(bounds.Height * s, 1));
    }

    private static string ColorToHex(SKColor c) =>
        $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
}
