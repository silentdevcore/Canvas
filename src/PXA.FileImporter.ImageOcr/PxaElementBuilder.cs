using PXA.Core.Contracts;
using SkiaSharp;

namespace PXA.FileImporter.ImageOcr;

// Pipeline stage 4: converts fused semantic candidates into Canvas ElementDto
// objects, applying page placement/scaling and source diagnostics to styles.
// Promoted verbatim from ImageToPdfConverter.BuildDesign element construction.
internal static class PxaElementBuilder
{
    public static ImagePlacement ResolveImagePlacement(
        int imageWidthPx,
        int imageHeightPx,
        double pageWidth,
        double pageHeight)
    {
        var scale = Math.Min(pageWidth / imageWidthPx, pageHeight / imageHeightPx);
        var width = imageWidthPx * scale;
        var height = imageHeightPx * scale;

        return new ImagePlacement(
            (pageWidth - width) / 2.0,
            (pageHeight - height) / 2.0,
            width,
            height,
            scale);
    }

    public static ElementDto BuildBackgroundImageElement(string dataUri, ImagePlacement placement) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "image",
            Name = "Original image background",
            X = Math.Round(placement.X, 2),
            Y = Math.Round(placement.Y, 2),
            Width = Math.Round(placement.Width, 2),
            Height = Math.Round(placement.Height, 2),
            Content = dataUri,
            FitMode = "fill",
            Locked = true,
            Style = new Dictionary<string, object>
            {
                ["imageOcrRole"] = "background",
            },
        };

    public static IReadOnlyList<ElementDto> BuildTextElements(
        OcrTextGroup textGroup,
        ImagePlacement placement,
        OcrPixels bitmap,
        double baselineHeightPx = 0,
        bool splitRuns = true)
    {
        // Text-only path (splitRuns == false): keep words together but split each line at large
        // horizontal gaps so spatially separate clusters (columns, left/right-aligned text that
        // OCR merged onto one line) become independent, correctly-positioned text elements.
        if (!splitRuns)
            return BuildGapSegmentedElements(textGroup, placement, bitmap, baselineHeightPx);

        var runs = BuildTextRuns(textGroup, placement, bitmap, baselineHeightPx);
        if (runs.Count <= 1)
            return [BuildTextElement(textGroup, placement, bitmap, baselineHeightPx)];

        return runs.Select(run => BuildTextRunElement(run, textGroup)).ToList();
    }

    // Splits each line's words into segments wherever the gap between consecutive words exceeds
    // ~1.1x the line's text height (a real column boundary, not a normal inter-word space), and
    // emits one positioned text element per segment.
    private static IReadOnlyList<ElementDto> BuildGapSegmentedElements(
        OcrTextGroup textGroup,
        ImagePlacement placement,
        OcrPixels bitmap,
        double baselineHeightPx)
    {
        var elements = new List<ElementDto>();
        foreach (var line in textGroup.Lines)
        {
            var words = line.Words
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .OrderBy(w => w.Bounds.X)
                .ToList();

            if (words.Count == 0)
            {
                // No word boxes: emit the whole line as a single element.
                elements.Add(BuildTextElement(
                    new OcrTextGroup([line], textGroup.Role, textGroup.ColumnIndex, textGroup.ColumnCount),
                    placement, bitmap, baselineHeightPx));
                continue;
            }

            foreach (var segment in SegmentWordsByGap(words))
                elements.Add(BuildTextRunElement(
                    BuildTextRun(line, segment, placement, bitmap, baselineHeightPx), textGroup));
        }

        return elements;
    }

    private static IEnumerable<List<OcrWord>> SegmentWordsByGap(IReadOnlyList<OcrWord> words)
    {
        var avgHeight = words.Average(w => Math.Max(1, w.Bounds.Height));
        var gapThreshold = Math.Max(8.0, avgHeight * 1.1);

        var current = new List<OcrWord> { words[0] };
        for (var i = 1; i < words.Count; i++)
        {
            var prev = words[i - 1];
            var gap = words[i].Bounds.X - (prev.Bounds.X + prev.Bounds.Width);
            if (gap > gapThreshold)
            {
                yield return current;
                current = [];
            }

            current.Add(words[i]);
        }

        yield return current;
    }

    // Clamp a raw OCR-box height to a sane band around the document baseline so a single
    // outlier-tall box cannot drive font size to the cap. A non-positive baseline disables it.
    private static double ClampHeightToBaseline(double rawHeightPx, double baselineHeightPx) =>
        baselineHeightPx > 0
            ? Math.Clamp(rawHeightPx, baselineHeightPx * 0.6, baselineHeightPx * 1.8)
            : rawHeightPx;

    private static ElementDto BuildTextElement(OcrTextGroup textGroup, ImagePlacement placement, OcrPixels bitmap, double baselineHeightPx = 0)
    {
        var bounds = OcrLayoutHelpers.UnionBounds(textGroup.Lines.Select(l => l.Bounds));
        var x = placement.X + bounds.X * placement.Scale;
        var y = placement.Y + bounds.Y * placement.Scale;
        var width = Math.Max(1, bounds.Width * placement.Scale);
        var height = Math.Max(1, bounds.Height * placement.Scale);
        var rawLineHeight = textGroup.Lines.Average(l => l.Bounds.Height);
        var averageLineHeight = ClampHeightToBaseline(rawLineHeight, baselineHeightPx) * placement.Scale;
        var fontSize = Math.Clamp(averageLineHeight * 0.78, 6, 72);
        var textColor = EstimateTextColor(bitmap, bounds);
        var role = ToTextRoleName(textGroup.Role);
        var fontWeight = textGroup.Role == OcrTextRole.Heading ? "700" : "normal";

        return new ElementDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "text",
            Name = textGroup.Role == OcrTextRole.Heading
                ? "OCR heading"
                : textGroup.Lines.Count > 1 ? "OCR paragraph" : "OCR text",
            X = Math.Round(x, 2),
            Y = Math.Round(y, 2),
            Width = Math.Round(width, 2),
            Height = Math.Round(height, 2),
            Content = string.Join("\n", textGroup.Lines.Select(l => l.Text)),
            Style = new Dictionary<string, object>
            {
                ["fontSize"] = Math.Round(fontSize, 2),
                ["lineHeight"] = 1.2,
                ["color"] = textColor,
                ["fontWeight"] = fontWeight,
                ["imageOcrConfidence"] = Math.Round(textGroup.Lines.Average(l => l.Confidence), 4),
                ["imageOcrRole"] = textGroup.Lines.Count > 1 ? "paragraph" : "text",
                ["imageOcrDetector"] = "ocr-text",
                ["imageOcrTextRole"] = role,
                ["sourceLineCount"] = textGroup.Lines.Count,
                ["sourceColumnIndex"] = textGroup.ColumnIndex,
                ["sourceColumnCount"] = textGroup.ColumnCount,
                ["sourceBoundsPx"] = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}",
            },
        };
    }

    private static ElementDto BuildTextRunElement(OcrTextRun run, OcrTextGroup textGroup)
    {
        var role = ToTextRoleName(textGroup.Role);
        var fontWeight = textGroup.Role == OcrTextRole.Heading ? "700" : "normal";

        return new ElementDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "text",
            Name = "OCR text run",
            X = Math.Round(run.X, 2),
            Y = Math.Round(run.Y, 2),
            Width = Math.Round(run.Width, 2),
            Height = Math.Round(run.Height, 2),
            Content = run.Text,
            Style = new Dictionary<string, object>
            {
                ["fontSize"] = Math.Round(run.FontSize, 2),
                ["lineHeight"] = 1.2,
                ["color"] = run.Color,
                ["fontWeight"] = fontWeight,
                ["imageOcrConfidence"] = Math.Round(run.Confidence, 4),
                ["imageOcrRole"] = "text-run",
                ["imageOcrDetector"] = "ocr-text-run",
                ["imageOcrTextRole"] = role,
                ["imageOcrRunSplit"] = true,
                ["sourceLineCount"] = 1,
                ["sourceColumnIndex"] = textGroup.ColumnIndex,
                ["sourceColumnCount"] = textGroup.ColumnCount,
                ["sourceBoundsPx"] = $"{run.SourceBounds.X},{run.SourceBounds.Y},{run.SourceBounds.Width},{run.SourceBounds.Height}",
            },
        };
    }

    private static IReadOnlyList<OcrTextRun> BuildTextRuns(
        OcrTextGroup textGroup,
        ImagePlacement placement,
        OcrPixels bitmap,
        double baselineHeightPx)
    {
        var allRuns = new List<OcrTextRun>();
        var anyLineSplit = false;

        foreach (var line in textGroup.Lines)
        {
            var words = line.Words
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .OrderBy(w => w.Bounds.X)
                .ToList();
            if (words.Count == 0)
                continue;
            if (words.Count == 1)
            {
                allRuns.Add(BuildTextRun(line, words, placement, bitmap, baselineHeightPx));
                continue;
            }

            var lineRuns = BuildLineTextRuns(line, words, placement, bitmap, baselineHeightPx);
            if (lineRuns.Count > 1)
                anyLineSplit = true;
            allRuns.AddRange(lineRuns);
        }

        return anyLineSplit ? allRuns : [];
    }

    private static IReadOnlyList<OcrTextRun> BuildLineTextRuns(
        OcrLine line,
        IReadOnlyList<OcrWord> words,
        ImagePlacement placement,
        OcrPixels bitmap,
        double baselineHeightPx)
    {
        var runs = new List<List<OcrWord>>();
        var current = new List<OcrWord> { words[0] };
        var currentColor = EstimateTextColor(bitmap, words[0].Bounds);
        var currentHeight = (double)Math.Max(1, words[0].Bounds.Height);

        for (var i = 1; i < words.Count; i++)
        {
            var word = words[i];
            var wordColor = EstimateTextColor(bitmap, word.Bounds);
            var wordHeight = Math.Max(1, word.Bounds.Height);

            if (IsDifferentTextRun(currentColor, currentHeight, wordColor, wordHeight))
            {
                runs.Add(current);
                current = [word];
                currentColor = wordColor;
                currentHeight = wordHeight;
                continue;
            }

            current.Add(word);
            currentColor = EstimateRunColor(current.Select(w => EstimateTextColor(bitmap, w.Bounds)));
            currentHeight = current.Average(w => Math.Max(1, w.Bounds.Height));
        }

        runs.Add(current);
        return runs.Select(runWords => BuildTextRun(line, runWords, placement, bitmap, baselineHeightPx)).ToList();
    }

    private static OcrTextRun BuildTextRun(
        OcrLine line,
        IReadOnlyList<OcrWord> words,
        ImagePlacement placement,
        OcrPixels bitmap,
        double baselineHeightPx)
    {
        var bounds = OcrLayoutHelpers.UnionBounds(words.Select(w => w.Bounds));
        var x = placement.X + bounds.X * placement.Scale;
        var y = placement.Y + bounds.Y * placement.Scale;
        var width = Math.Max(1, bounds.Width * placement.Scale);
        var height = Math.Max(1, bounds.Height * placement.Scale);
        var rawWordHeight = words.Average(w => Math.Max(1, w.Bounds.Height));
        var averageWordHeight = ClampHeightToBaseline(rawWordHeight, baselineHeightPx) * placement.Scale;
        var fontSize = Math.Clamp(averageWordHeight * 0.78, 6, 72);
        var color = EstimateRunColor(words.Select(w => EstimateTextColor(bitmap, w.Bounds)));
        var text = RebuildRunText(line, words);

        return new OcrTextRun(
            text,
            x,
            y,
            width,
            height,
            fontSize,
            color,
            words.Average(w => w.Confidence),
            bounds);
    }

    private static string RebuildRunText(OcrLine line, IReadOnlyList<OcrWord> words)
    {
        if (words.Count == line.Words.Count)
            return line.Text;

        return string.Join(" ", words.Select(w => w.Text));
    }

    private static bool IsDifferentTextRun(string currentColor, double currentHeight, string wordColor, double wordHeight)
    {
        var heightRatio = Math.Min(currentHeight, wordHeight) / Math.Max(currentHeight, wordHeight);
        if (heightRatio < 0.80)
            return true;

        return ColorDistance(currentColor, wordColor) >= 90;
    }

    private static string EstimateRunColor(IEnumerable<string> colors)
    {
        var grouped = colors
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        return grouped?.Key ?? "#111827";
    }

    private static int ColorDistance(string a, string b)
    {
        if (!TryParseHexColor(a, out var ar, out var ag, out var ab) ||
            !TryParseHexColor(b, out var br, out var bg, out var bb))
            return 0;

        return Math.Abs(ar - br) + Math.Abs(ag - bg) + Math.Abs(ab - bb);
    }

    private static bool TryParseHexColor(string value, out int red, out int green, out int blue)
    {
        red = 0;
        green = 0;
        blue = 0;
        if (value.Length != 7 || value[0] != '#')
            return false;

        red = Convert.ToInt32(value[1..3], 16);
        green = Convert.ToInt32(value[3..5], 16);
        blue = Convert.ToInt32(value[5..7], 16);
        return true;
    }

    public static ElementDto BuildShapeElement(OcrShapeCandidate shape, ImagePlacement placement)
    {
        var x = placement.X + shape.Bounds.X * placement.Scale;
        var y = placement.Y + shape.Bounds.Y * placement.Scale;
        var width = Math.Max(1, shape.Bounds.Width * placement.Scale);
        var height = Math.Max(1, shape.Bounds.Height * placement.Scale);
        var strokeWidth = Math.Max(0.75, Math.Min(width, height));

        if (shape.Kind is OcrShapeKind.Circle or OcrShapeKind.Ellipse)
        {
            var kind = shape.Kind == OcrShapeKind.Circle ? "circle" : "ellipse";
            return new ElementDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "circle",
                Name = shape.Kind == OcrShapeKind.Circle ? "OCR circle" : "OCR ellipse",
                X = Math.Round(x, 2),
                Y = Math.Round(y, 2),
                Width = Math.Round(width, 2),
                Height = Math.Round(height, 2),
                Style = new Dictionary<string, object>
                {
                    ["backgroundColor"] = "transparent",
                    ["borderColor"] = "#111827",
                    ["borderWidth"] = 0.75,
                    ["imageOcrRole"] = "shape",
                    ["imageOcrShapeKind"] = kind,
                    ["imageOcrConfidence"] = 0.80,
                    ["imageOcrDetector"] = "oval-contour",
                    ["sourceBoundsPx"] = $"{shape.Bounds.X},{shape.Bounds.Y},{shape.Bounds.Width},{shape.Bounds.Height}",
                },
            };
        }

        if (shape.Kind is OcrShapeKind.Rectangle or OcrShapeKind.FilledRectangle)
        {
            var filled = shape.Kind == OcrShapeKind.FilledRectangle;
            var fillColor = filled ? shape.FillColor ?? "#111827" : "transparent";
            var borderColor = filled ? "transparent" : "#111827";
            return new ElementDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "rect",
                Name = filled ? "OCR filled rectangle" : "OCR rectangle",
                X = Math.Round(x, 2),
                Y = Math.Round(y, 2),
                Width = Math.Round(width, 2),
                Height = Math.Round(height, 2),
                Style = new Dictionary<string, object>
                {
                    ["backgroundColor"] = fillColor,
                    ["borderColor"] = borderColor,
                    ["borderWidth"] = filled ? 0 : 0.75,
                    ["imageOcrRole"] = "shape",
                    ["imageOcrShapeKind"] = filled ? "filled-rectangle" : "rectangle",
                    ["imageOcrConfidence"] = filled ? 0.82 : 0.86,
                    ["imageOcrDetector"] = filled ? "connected-fill" : "rule-rectangle",
                    ["sourceBoundsPx"] = $"{shape.Bounds.X},{shape.Bounds.Y},{shape.Bounds.Width},{shape.Bounds.Height}",
                },
            };
        }

        return new ElementDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "line",
            Name = "OCR line",
            X = Math.Round(x, 2),
            Y = Math.Round(y, 2),
            Width = Math.Round(width, 2),
            Height = Math.Round(height, 2),
            Style = new Dictionary<string, object>
            {
                ["color"] = "#111827",
                ["strokeWidth"] = Math.Round(strokeWidth, 2),
                ["imageOcrRole"] = "shape",
                ["imageOcrShapeKind"] = shape.Kind == OcrShapeKind.HorizontalLine ? "horizontal-line" : "vertical-line",
                ["imageOcrConfidence"] = 0.82,
                ["imageOcrDetector"] = "rule-line",
                ["sourceBoundsPx"] = $"{shape.Bounds.X},{shape.Bounds.Y},{shape.Bounds.Width},{shape.Bounds.Height}",
            },
        };
    }

    public static ElementDto BuildCheckboxElement(OcrCheckboxCandidate checkbox, ImagePlacement placement)
    {
        var x = placement.X + checkbox.Bounds.X * placement.Scale;
        var y = placement.Y + checkbox.Bounds.Y * placement.Scale;
        var width = Math.Max(1, checkbox.Bounds.Width * placement.Scale);
        var height = Math.Max(1, checkbox.Bounds.Height * placement.Scale);

        return new ElementDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "checkbox",
            Name = "OCR checkbox",
            X = Math.Round(x, 2),
            Y = Math.Round(y, 2),
            Width = Math.Round(width, 2),
            Height = Math.Round(height, 2),
            CheckState = checkbox.State,
            Style = new Dictionary<string, object>
            {
                ["borderColor"] = "#111827",
                ["backgroundColor"] = "#ffffff",
                ["color"] = "#111827",
                ["imageOcrRole"] = "checkbox",
                ["imageOcrConfidence"] = checkbox.Confidence,
                ["imageOcrDetector"] = "rule-square",
                ["sourceBoundsPx"] = $"{checkbox.Bounds.X},{checkbox.Bounds.Y},{checkbox.Bounds.Width},{checkbox.Bounds.Height}",
            },
        };
    }

    public static ElementDto BuildFieldElement(OcrFieldCandidate field, ImagePlacement placement)
    {
        var x = placement.X + field.Bounds.X * placement.Scale;
        var y = placement.Y + field.Bounds.Y * placement.Scale;
        var width = Math.Max(1, field.Bounds.Width * placement.Scale);
        var height = Math.Max(1, field.Bounds.Height * placement.Scale);
        var label = field.LabelLine.Text.Trim();

        return new ElementDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "field",
            Name = "OCR form field",
            X = Math.Round(x, 2),
            Y = Math.Round(y, 2),
            Width = Math.Round(width, 2),
            Height = Math.Round(height, 2),
            FieldLabel = label,
            FieldName = ToFieldName(label),
            Placeholder = string.Empty,
            Style = new Dictionary<string, object>
            {
                ["borderColor"] = "#111827",
                ["backgroundColor"] = "#ffffff",
                ["color"] = "#111827",
                ["fontSize"] = Math.Round(Math.Clamp(field.LabelLine.Bounds.Height * placement.Scale * 0.75, 6, 14), 2),
                ["imageOcrRole"] = "form-field",
                ["imageOcrConfidence"] = field.Confidence,
                ["imageOcrDetector"] = "labeled-rectangle",
                ["imageOcrLabelBoundsPx"] = $"{field.LabelLine.Bounds.X},{field.LabelLine.Bounds.Y},{field.LabelLine.Bounds.Width},{field.LabelLine.Bounds.Height}",
                ["sourceBoundsPx"] = $"{field.Bounds.X},{field.Bounds.Y},{field.Bounds.Width},{field.Bounds.Height}",
            },
        };
    }

    public static ElementDto BuildSignatureElement(OcrSignatureCandidate signature, ImagePlacement placement)
    {
        const int lineOffsetPx = 10;
        const int elementHeightPx = 24;
        var x = placement.X + signature.Bounds.X * placement.Scale;
        var y = placement.Y + Math.Max(0, signature.Bounds.Y - lineOffsetPx) * placement.Scale;
        var width = Math.Max(1, signature.Bounds.Width * placement.Scale);
        var height = Math.Max(1, elementHeightPx * placement.Scale);
        var label = signature.LabelLine.Text.Trim();

        return new ElementDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "signature",
            Name = "OCR signature",
            X = Math.Round(x, 2),
            Y = Math.Round(y, 2),
            Width = Math.Round(width, 2),
            Height = Math.Round(height, 2),
            SignatureLabel = label,
            Style = new Dictionary<string, object>
            {
                ["borderColor"] = "#111827",
                ["color"] = "#111827",
                ["labelColor"] = "#6b7280",
                ["imageOcrRole"] = "signature",
                ["imageOcrConfidence"] = signature.Confidence,
                ["imageOcrDetector"] = "labeled-line",
                ["imageOcrLabelBoundsPx"] = $"{signature.LabelLine.Bounds.X},{signature.LabelLine.Bounds.Y},{signature.LabelLine.Bounds.Width},{signature.LabelLine.Bounds.Height}",
                ["sourceBoundsPx"] = $"{signature.Bounds.X},{signature.Bounds.Y},{signature.Bounds.Width},{signature.Bounds.Height}",
            },
        };
    }

    public static ElementDto BuildImageRegionElement(OcrImageRegionCandidate region, ImagePlacement placement, SKBitmap bitmap)
    {
        var x = placement.X + region.Bounds.X * placement.Scale;
        var y = placement.Y + region.Bounds.Y * placement.Scale;
        var width = Math.Max(1, region.Bounds.Width * placement.Scale);
        var height = Math.Max(1, region.Bounds.Height * placement.Scale);

        return new ElementDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "image",
            Name = "OCR image region",
            X = Math.Round(x, 2),
            Y = Math.Round(y, 2),
            Width = Math.Round(width, 2),
            Height = Math.Round(height, 2),
            Content = EncodeImageRegion(bitmap, region.Bounds),
            FitMode = "fill",
            Locked = false,
            Style = new Dictionary<string, object>
            {
                ["imageOcrRole"] = "image-region",
                ["imageOcrConfidence"] = region.Confidence,
                ["imageOcrDetector"] = "connected-region",
                ["sourceBoundsPx"] = $"{region.Bounds.X},{region.Bounds.Y},{region.Bounds.Width},{region.Bounds.Height}",
            },
        };
    }

    public static ElementDto BuildTableElement(OcrTableCandidate table, ImagePlacement placement, OcrPixels bitmap)
    {
        var wordBounds = OcrLayoutHelpers.UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds)));
        var bounds = table.RuleBounds ?? table.BackgroundBounds ?? wordBounds;
        var paddingPx = Math.Max(2, table.Lines.Average(l => l.Bounds.Height) * 0.35);
        var useVisualBounds = table.RuleBounds is not null || table.BackgroundBounds is not null;
        var useRuleBounds = table.RuleBounds is not null;
        var x = placement.X + Math.Max(0, bounds.X - (useVisualBounds ? 0 : paddingPx)) * placement.Scale;
        var y = placement.Y + Math.Max(0, bounds.Y - (useVisualBounds ? 0 : paddingPx)) * placement.Scale;
        var width = (bounds.Width + (useVisualBounds ? 0 : paddingPx * 2)) * placement.Scale;
        var height = (bounds.Height + (useVisualBounds ? 0 : paddingPx * 2)) * placement.Scale;

        var cellData = BuildTableCellData(table);
        var columnWidths = BuildTableColumnWidths(table);
        var textColor = EstimateTextColor(bitmap, OcrLayoutHelpers.UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds))));

        return new ElementDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "table",
            Name = "OCR table",
            X = Math.Round(x, 2),
            Y = Math.Round(y, 2),
            Width = Math.Round(width, 2),
            Height = Math.Round(height, 2),
            CellData = cellData,
            ColumnWidths = columnWidths,
            HeaderRow = HasLikelyHeaderRow(cellData),
            HeaderBgColor = "#f1f5f9",
            ZebraEnabled = false,
            Style = new Dictionary<string, object>
            {
                ["rows"] = cellData.Length,
                ["columns"] = table.ColumnAnchors.Count,
                ["fontSize"] = Math.Round(Math.Clamp(table.Lines.Average(l => l.Bounds.Height) * placement.Scale * 0.68, 6, 18), 2),
                ["color"] = textColor,
                ["borderColor"] = "#9ca3af",
                ["borderWidth"] = 0.75,
                ["cellPadding"] = 3,
                ["imageOcrRole"] = "table",
                ["imageOcrConfidence"] = Math.Round(table.Lines.Average(l => l.Confidence), 4),
                ["imageOcrDetector"] = useRuleBounds ? "rule-bounded-table" : "aligned-text-table",
                ["imageOcrRuleBounded"] = useRuleBounds,
                ["imageOcrBackgroundBounded"] = !useRuleBounds && table.BackgroundBounds is not null,
                ["sourceBoundsPx"] = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}",
            },
        };
    }

    private static string[][] BuildTableCellData(OcrTableCandidate table)
    {
        var tolerance = OcrLayoutHelpers.EstimateTableColumnTolerance(table);
        return table.RowGroups
            .Select(row =>
            {
                var cells = Enumerable.Repeat(string.Empty, table.ColumnAnchors.Count).ToArray();
                foreach (var word in row.SelectMany(line => line.Words).Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderBy(w => w.Bounds.X))
                {
                    var column = OcrLayoutHelpers.FindNearestColumn(word.Bounds.X + word.Bounds.Width / 2.0, table.ColumnAnchors, tolerance);
                    if (column < 0)
                        continue;

                    cells[column] = string.IsNullOrWhiteSpace(cells[column])
                        ? word.Text
                        : $"{cells[column]} {word.Text}";
                }

                return cells;
            })
            .ToArray();
    }

    private static double[] BuildTableColumnWidths(OcrTableCandidate table)
    {
        var widths = new double[table.ColumnAnchors.Count];
        for (var column = 0; column < widths.Length; column++)
        {
            var wordWidths = table.Lines
                .SelectMany(line => line.Words)
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .Where(w => OcrLayoutHelpers.FindNearestColumn(w.Bounds.X + w.Bounds.Width / 2.0, table.ColumnAnchors, OcrLayoutHelpers.EstimateTableColumnTolerance(table)) == column)
                .Select(w => (double)w.Bounds.Width)
                .ToArray();

            if (wordWidths.Length > 0)
                widths[column] = wordWidths.Average();
            else if (column < table.ColumnAnchors.Count - 1)
                widths[column] = Math.Max(1, table.ColumnAnchors[column + 1] - table.ColumnAnchors[column]);
            else if (column > 0)
                widths[column] = Math.Max(1, table.ColumnAnchors[column] - table.ColumnAnchors[column - 1]);
            else
                widths[column] = 1;
        }

        return widths;
    }

    private static bool HasLikelyHeaderRow(string[][] cellData)
    {
        if (cellData.Length < 2)
            return false;

        var firstRow = cellData[0];
        var remaining = cellData.Skip(1).SelectMany(r => r);
        return firstRow.Any(c => c.Any(char.IsLetter)) &&
               remaining.Any(c => c.Any(char.IsDigit));
    }

    private static string ToFieldName(string label)
    {
        var parts = label
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        var normalized = new string(parts).Trim('_');
        while (normalized.Contains("__", StringComparison.Ordinal))
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(normalized) ? "field" : normalized;
    }

    private static string ToTextRoleName(OcrTextRole role) =>
        role switch
        {
            OcrTextRole.Heading => "heading",
            OcrTextRole.Caption => "caption",
            _ => "body",
        };

    private static string EstimateTextColor(OcrPixels bitmap, OcrBoundingBox bounds)
    {
        var left = Math.Clamp(bounds.X - 1, 0, Math.Max(0, bitmap.Width - 1));
        var top = Math.Clamp(bounds.Y - 1, 0, Math.Max(0, bitmap.Height - 1));
        var right = Math.Clamp(bounds.X + bounds.Width + 1, 0, bitmap.Width);
        var bottom = Math.Clamp(bounds.Y + bounds.Height + 1, 0, bitmap.Height);
        if (right <= left || bottom <= top)
            return "#111827";

        var samples = new List<SKColor>();
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (OcrLayoutHelpers.IsLikelyTextPixel(color))
                    samples.Add(color);
            }
        }

        if (samples.Count < 3)
            return "#111827";

        var red = OcrLayoutHelpers.Median(samples.Select(c => c.Red));
        var green = OcrLayoutHelpers.Median(samples.Select(c => c.Green));
        var blue = OcrLayoutHelpers.Median(samples.Select(c => c.Blue));
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static string EncodeImageRegion(SKBitmap bitmap, OcrBoundingBox bounds)
    {
        var left = Math.Clamp(bounds.X, 0, Math.Max(0, bitmap.Width - 1));
        var top = Math.Clamp(bounds.Y, 0, Math.Max(0, bitmap.Height - 1));
        var right = Math.Clamp(bounds.X + bounds.Width, left + 1, bitmap.Width);
        var bottom = Math.Clamp(bounds.Y + bounds.Height, top + 1, bitmap.Height);
        using var crop = new SKBitmap(right - left, bottom - top, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(crop))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(
                bitmap,
                new SKRect(left, top, right, bottom),
                new SKRect(0, 0, crop.Width, crop.Height));
        }

        using var image = SKImage.FromBitmap(crop);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return $"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
    }
}
