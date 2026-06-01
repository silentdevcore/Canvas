using Canvas.Core.Contracts;
using SkiaSharp;

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
        var gridLineBounds = DetectGridLineBounds(shapes.Shapes);

        // Z-order: colour regions (bottom) → shapes → text (top)
        foreach (var region in colors.Regions)
        {
            all.Add(new ImageRegionPrimitive
            {
                Bounds    = region.Bounds,
                FillColor = region.FillColor,
                Coverage  = region.Coverage,
                ZOrder    = z++,
            });
        }

        foreach (var shape in shapes.Shapes)
        {
            // Suppress shapes/lines inside text regions or dark colour regions
            // (character-stroke artefacts from undetected white/coloured-on-dark text).
            if (IsTextStrokeArtifact(shape, textRegions) ||
                OverlapsTextRegion(shape.Bounds, darkRegions)) continue;

            if (IsDuplicateOfExistingRegion(all, shape)) continue;

            // Suppress colour regions fully covered by a matching-fill shape
            RemoveCoveredRegions(all, shape.Bounds);

            all.Add(new ImageShapePrimitive
            {
                Bounds      = shape.Bounds,
                Kind        = shape.Kind,
                FillColor   = shape.FillColor,
                StrokeColor = shape.StrokeColor,
                StrokeWidth = shape.StrokeWidth,
                Confidence  = shape.Confidence,
                AnalysisType = gridLineBounds.Contains(shape.Bounds) ? "grid-line" : shape.AnalysisType,
                ZOrder      = z++,
            });
        }

        foreach (var line in AssignTextBlocks(texts.Lines))
        {
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
            IsMostlyCovered(r.Bounds, shapeBounds));
    }

    private static bool IsDuplicateOfExistingRegion(List<ImagePrimitive> primitives, ImageShapePrimitive shape)
    {
        if (shape.Kind != ShapeKind.Rect) return false;
        if (shape.FillColor == SKColors.Transparent) return false;

        return primitives.Any(p =>
            p is ImageRegionPrimitive region &&
            IsMostlyCovered(region.Bounds, shape.Bounds) &&
            IsMostlyCovered(shape.Bounds, region.Bounds) &&
            ColorAnalyzer.ColorDistance(region.FillColor, shape.FillColor) <= ColorAnalyzer.ColorTolerance);
    }

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

    private static (int Width, int Height, int Area) Overlap(SKRectI a, SKRectI b)
    {
        int width = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
        int height = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
        return (width, height, width * height);
    }

    private static HashSet<SKRectI> DetectGridLineBounds(IReadOnlyList<ImageShapePrimitive> shapes)
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

        return gridLines;
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
        var blockLineCounts = new Dictionary<int, int>();
        int nextBlockId = 1;

        foreach (var line in sorted)
        {
            int? blockId = FindTextBlock(line, blockLastLine);
            if (blockId is null)
            {
                blockId = nextBlockId++;
                blockLineCounts[blockId.Value] = 0;
            }

            int lineIndex = blockLineCounts[blockId.Value]++;
            var copy = CopyTextLine(line, blockId.Value, lineIndex);
            assigned.Add(copy);
            blockLastLine[blockId.Value] = copy;
        }

        return assigned
            .OrderBy(l => l.Bounds.Top)
            .ThenBy(l => l.Bounds.Left)
            .ToList();
    }

    private static int? FindTextBlock(
        ImageTextPrimitive line,
        Dictionary<int, ImageTextPrimitive> blockLastLine)
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

            if (!LooksLikeSameTextBlock(previous, line))
                continue;

            if (verticalGap < bestGap)
            {
                bestGap = verticalGap;
                bestId = blockId;
            }
        }

        return bestId;
    }

    private static bool LooksLikeSameTextBlock(ImageTextPrimitive previous, ImageTextPrimitive current)
    {
        int overlap = Math.Max(0, Math.Min(previous.Bounds.Right, current.Bounds.Right) -
                                  Math.Max(previous.Bounds.Left, current.Bounds.Left));
        int minWidth = Math.Max(1, Math.Min(previous.Bounds.Width, current.Bounds.Width));
        double overlapRatio = (double)overlap / minWidth;
        double leftDelta = Math.Abs(previous.Bounds.Left - current.Bounds.Left);
        double tolerance = Math.Max(previous.FontSizePx, current.FontSizePx) * 2.5;

        return overlapRatio >= 0.25 || leftDelta <= tolerance;
    }

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
            pageW = srcW;
            pageH = srcH;
            s     = 1.0 / scaleFactor;
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
                BackgroundColor = ColorToHex(background),
                Margins     = new MarginsDto { Top = 0, Right = 0, Bottom = 0, Left = 0 },
            },
        };
    }

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
                        ["imageAnalysisType"] = "color-region",
                    },
                    region.Bounds,
                    confidence: 0.90,
                    lowConfidenceThreshold),
            },

            ImageShapePrimitive shape when shape.Kind == ShapeKind.Line => new ElementDto
            {
                Id     = $"line-{seq++}", Type = "rect",
                X      = x, Y = y, Width = Math.Max(0.5, w), Height = Math.Max(0.5, h),
                Style  = WithAnalysisMetadata(
                    new Dictionary<string, object>
                    {
                        ["backgroundColor"] = ColorToHex(shape.StrokeColor),
                        ["borderWidth"]     = 0,
                        ["imageAnalysisType"] = shape.AnalysisType ?? "line",
                    },
                    shape.Bounds,
                    shape.Confidence,
                    lowConfidenceThreshold),
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
        var style = WithAnalysisMetadata(new Dictionary<string, object>
        {
            ["backgroundColor"]   = shape.FillColor == SKColors.Transparent
                ? "transparent" : ColorToHex(shape.FillColor),
            ["borderColor"]       = shape.StrokeColor == SKColors.Transparent
                ? "transparent" : ColorToHex(shape.StrokeColor),
            ["borderWidth"]       = shape.StrokeWidth,
            ["borderStyle"]       = "solid",
            ["imageAnalysisType"] = shape.AnalysisType ?? shape.Kind.ToString().ToLowerInvariant(),
        }, shape.Bounds, shape.Confidence, lowConfidenceThreshold);
        if (shape.Kind == ShapeKind.Ellipse)
            style["borderRadius"] = "50%";
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
