using Canvas.Core.Contracts;
using SkiaSharp;
using System.Diagnostics;

namespace Canvas.FileImporter.ImageOcr;

public sealed class ImageToPdfConverter
{
    private const double DefaultDpi = 300;

    private readonly IOcrEngine _ocrEngine;

    public ImageToPdfConverter(IOcrEngine ocrEngine)
    {
        _ocrEngine = ocrEngine;
    }

    public async Task<ImageToPdfConversionResult> ConvertAsync(
        Stream stream,
        string? fileName,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        var memoryBefore = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();
        var raw = await ReadAllBytesAsync(stream, cancellationToken);
        if (raw.LongLength > options.MaxFileBytes)
            throw new InvalidOperationException($"Image file is too large. Maximum allowed size is {options.MaxFileBytes} bytes.");

        var sourceName = string.IsNullOrWhiteSpace(fileName) ? "image-ocr" : Path.GetFileNameWithoutExtension(fileName);

        using var codec = SKCodec.Create(new SKMemoryStream(raw))
            ?? throw new InvalidOperationException("Unable to decode image. The file is unsupported or corrupt.");

        using var decoded = SKBitmap.Decode(codec)
            ?? throw new InvalidOperationException("Unable to decode image. The file is unsupported or corrupt.");

        var metadataDpi = ReadImageDpi(raw, codec.EncodedOrigin);
        using var bitmap = ApplyOrientation(decoded, codec.EncodedOrigin);
        var pixelCount = (long)bitmap.Width * bitmap.Height;
        if (pixelCount > options.MaxPixels)
            throw new InvalidOperationException($"Image pixel count is too large. Maximum allowed pixel count is {options.MaxPixels}.");

        var (dataUri, originalEncoded) = EncodeImage(bitmap);
        using var ocrBitmap = PreprocessForOcr(bitmap, options, out var preprocessingSteps);
        var encodedForOcr = preprocessingSteps.Count == 0 ? originalEncoded : EncodeImageBytes(ocrBitmap);

        var dpiX = NormalizeDpi(options.SourceDpiX) ?? metadataDpi.X ?? DefaultDpi;
        var dpiY = NormalizeDpi(options.SourceDpiY) ?? metadataDpi.Y ?? DefaultDpi;
        var (pageWidth, pageHeight) = ResolvePageSize(bitmap.Width, bitmap.Height, dpiX, dpiY, options);

        IReadOnlyList<OcrPage> ocrPages;
        try
        {
            ocrPages = await _ocrEngine.RecognizeAsync(
                [new OcrImagePage(0, bitmap.Width, bitmap.Height, encodedForOcr)],
                options,
                cancellationToken);
        }
        catch (DllNotFoundException ex)
        {
            throw new OcrNativeDependencyMissingException(
                "OCR native binaries could not be loaded. Bundle matching Tesseract and Leptonica native libraries with the app, or configure Ocr:NativeLibraryPath to an app-owned native library folder.",
                ex);
        }
        catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException dllEx)
        {
            throw new OcrNativeDependencyMissingException(
                "OCR native binaries could not be loaded. Bundle matching Tesseract and Leptonica native libraries with the app, or configure Ocr:NativeLibraryPath to an app-owned native library folder.",
                dllEx);
        }

        var design = BuildDesign(sourceName, dataUri, bitmap, pageWidth, pageHeight, ocrPages, options);
        stopwatch.Stop();
        var memoryAfter = GC.GetTotalMemory(false);

        var words = ocrPages.SelectMany(p => p.Blocks).SelectMany(b => b.Lines).SelectMany(l => l.Words).ToList();
        var lines = ocrPages.SelectMany(p => p.Blocks).SelectMany(b => b.Lines).ToList();
        var lowConfidenceWords = words.Count(w => w.Confidence < options.LowConfidenceThreshold);
        var warnings = BuildWarnings(dpiX, dpiY, words, lowConfidenceWords, options);

        return new ImageToPdfConversionResult
        {
            Design = design,
            OcrPages = ocrPages,
            Warnings = warnings,
            Diagnostics = new ImageToPdfDiagnostics
            {
                SourceWidthPx = bitmap.Width,
                SourceHeightPx = bitmap.Height,
                EffectiveDpiX = Math.Round(dpiX, 2),
                EffectiveDpiY = Math.Round(dpiY, 2),
                PageWidthPt = Math.Round(pageWidth, 2),
                PageHeightPt = Math.Round(pageHeight, 2),
                PreprocessingApplied = preprocessingSteps.Count > 0,
                PreprocessingScaleFactor = 1,
                PreprocessingSteps = preprocessingSteps,
                PageCount = ocrPages.Count,
                OcrEngine = _ocrEngine.Name,
                OcrEngineVersion = _ocrEngine.Version,
                Languages = options.Languages,
                WordCount = words.Count,
                LineCount = lines.Count,
                AverageConfidence = words.Count == 0 ? 0 : Math.Round(words.Average(w => w.Confidence), 4),
                LowConfidenceWordCount = lowConfidenceWords,
                RuntimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
                MemoryDeltaBytes = memoryAfter - memoryBefore,
            },
            DebugOverlayPng = options.IncludeDebugOverlay
                ? OcrDebugOverlayRenderer.Render(bitmap, ocrPages)
                : null,
        };
    }

    private static (double Width, double Height) ResolvePageSize(
        int imageWidthPx,
        int imageHeightPx,
        double dpiX,
        double dpiY,
        ImageToPdfConversionOptions options)
    {
        if (options.PageWidthPt is > 0 && options.PageHeightPt is > 0)
            return (options.PageWidthPt.Value, options.PageHeightPt.Value);

        if (string.Equals(options.PageSizingMode, "a4-fit", StringComparison.OrdinalIgnoreCase))
            return imageWidthPx >= imageHeightPx ? (842, 595) : (595, 842);

        return (imageWidthPx / dpiX * 72.0, imageHeightPx / dpiY * 72.0);
    }

    private static DesignExportDto BuildDesign(
        string name,
        string dataUri,
        SKBitmap bitmap,
        double pageWidth,
        double pageHeight,
        IReadOnlyList<OcrPage> ocrPages,
        ImageToPdfConversionOptions options)
    {
        var elements = new List<ElementDto>();
        var placement = ResolveImagePlacement(bitmap.Width, bitmap.Height, pageWidth, pageHeight);
        var lines = ocrPages
            .SelectMany(p => p.Blocks)
            .SelectMany(b => b.Lines)
            .Where(l => !string.IsNullOrWhiteSpace(l.Text))
            .OrderBy(l => l.Bounds.Y)
            .ThenBy(l => l.Bounds.X)
            .ToList();
        var ruleSegments = DetectRuleSegments(bitmap);
        var tableCandidates = DetectTables(lines, options)
            .Select(t => t with { RuleBounds = FindRuleBounds(t, ruleSegments) })
            .ToList();
        var tableLines = tableCandidates
            .SelectMany(t => t.Lines)
            .ToHashSet();
        var tableRuleBounds = tableCandidates
            .Select(t => t.RuleBounds)
            .Where(b => b is not null)
            .Cast<OcrBoundingBox>()
            .ToList();
        var shapeSegments = ShouldDetectShapes(options)
            ? ruleSegments.Where(s => !IsSegmentInsideAnyBounds(s, tableRuleBounds)).ToList()
            : [];
        var shapeCandidates = DetectShapes(shapeSegments);

        if (options.IncludeBackgroundImage)
        {
            elements.Add(new ElementDto
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
            });
        }

        foreach (var table in tableCandidates)
            elements.Add(BuildTableElement(table, placement, bitmap));

        foreach (var shape in shapeCandidates)
            elements.Add(BuildShapeElement(shape, placement));

        var textGroups = BuildTextGroups(lines.Where(l => !tableLines.Contains(l)).ToList(), options);
        foreach (var textGroup in textGroups)
            elements.AddRange(BuildTextElements(textGroup, placement, bitmap));

        return new DesignExportDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Pages =
            [
                new PageDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Elements = elements,
                }
            ],
            PageSettings = new PageSettingsDto
            {
                Width = Math.Round(pageWidth, 2),
                Height = Math.Round(pageHeight, 2),
                Orientation = pageWidth > pageHeight ? "landscape" : "portrait",
                Unit = "pt",
                Metadata = new PdfMetadataDto
                {
                    Title = name,
                    Subject = "Converted with Canvas Image OCR Converter",
                },
            },
        };
    }

    private static IReadOnlyList<ElementDto> BuildTextElements(OcrTextGroup textGroup, ImagePlacement placement, SKBitmap bitmap)
    {
        var runs = BuildTextRuns(textGroup, placement, bitmap);
        if (runs.Count <= 1)
            return [BuildTextElement(textGroup, placement, bitmap)];

        return runs.Select(run => BuildTextRunElement(run, textGroup)).ToList();
    }

    private static ElementDto BuildTextElement(OcrTextGroup textGroup, ImagePlacement placement, SKBitmap bitmap)
    {
        var bounds = UnionBounds(textGroup.Lines.Select(l => l.Bounds));
        var x = placement.X + bounds.X * placement.Scale;
        var y = placement.Y + bounds.Y * placement.Scale;
        var width = Math.Max(1, bounds.Width * placement.Scale);
        var height = Math.Max(1, bounds.Height * placement.Scale);
        var averageLineHeight = textGroup.Lines.Average(l => l.Bounds.Height) * placement.Scale;
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
        SKBitmap bitmap)
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
                allRuns.Add(BuildTextRun(line, words, placement, bitmap));
                continue;
            }

            var lineRuns = BuildLineTextRuns(line, words, placement, bitmap);
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
        SKBitmap bitmap)
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
        return runs.Select(runWords => BuildTextRun(line, runWords, placement, bitmap)).ToList();
    }

    private static OcrTextRun BuildTextRun(
        OcrLine line,
        IReadOnlyList<OcrWord> words,
        ImagePlacement placement,
        SKBitmap bitmap)
    {
        var bounds = UnionBounds(words.Select(w => w.Bounds));
        var x = placement.X + bounds.X * placement.Scale;
        var y = placement.Y + bounds.Y * placement.Scale;
        var width = Math.Max(1, bounds.Width * placement.Scale);
        var height = Math.Max(1, bounds.Height * placement.Scale);
        var averageWordHeight = words.Average(w => Math.Max(1, w.Bounds.Height)) * placement.Scale;
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

    private static ElementDto BuildShapeElement(OcrShapeCandidate shape, ImagePlacement placement)
    {
        var x = placement.X + shape.Bounds.X * placement.Scale;
        var y = placement.Y + shape.Bounds.Y * placement.Scale;
        var width = Math.Max(1, shape.Bounds.Width * placement.Scale);
        var height = Math.Max(1, shape.Bounds.Height * placement.Scale);
        var strokeWidth = Math.Max(0.75, Math.Min(width, height));

        if (shape.Kind == OcrShapeKind.Rectangle)
        {
            return new ElementDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "rect",
                Name = "OCR rectangle",
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
                    ["imageOcrShapeKind"] = "rectangle",
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
                ["sourceBoundsPx"] = $"{shape.Bounds.X},{shape.Bounds.Y},{shape.Bounds.Width},{shape.Bounds.Height}",
            },
        };
    }

    private static ElementDto BuildTableElement(OcrTableCandidate table, ImagePlacement placement, SKBitmap bitmap)
    {
        var wordBounds = UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds)));
        var bounds = table.RuleBounds ?? wordBounds;
        var paddingPx = Math.Max(2, table.Lines.Average(l => l.Bounds.Height) * 0.35);
        var useRuleBounds = table.RuleBounds is not null;
        var x = placement.X + Math.Max(0, bounds.X - (useRuleBounds ? 0 : paddingPx)) * placement.Scale;
        var y = placement.Y + Math.Max(0, bounds.Y - (useRuleBounds ? 0 : paddingPx)) * placement.Scale;
        var width = (bounds.Width + (useRuleBounds ? 0 : paddingPx * 2)) * placement.Scale;
        var height = (bounds.Height + (useRuleBounds ? 0 : paddingPx * 2)) * placement.Scale;

        var cellData = BuildTableCellData(table);
        var columnWidths = BuildTableColumnWidths(table);
        var textColor = EstimateTextColor(bitmap, UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds))));

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
                ["imageOcrRuleBounded"] = useRuleBounds,
                ["sourceBoundsPx"] = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}",
            },
        };
    }

    private static IReadOnlyList<OcrTextGroup> BuildTextGroups(
        IReadOnlyList<OcrLine> lines,
        ImageToPdfConversionOptions options)
    {
        if (lines.Count == 0)
            return [];

        var ordered = lines
            .OrderBy(l => l.Bounds.Y)
            .ThenBy(l => l.Bounds.X)
            .ToList();
        var typicalLineHeight = EstimateTypicalLineHeight(ordered);
        var roles = ordered.ToDictionary(l => l, l => ClassifyTextRole(l, typicalLineHeight));

        if (!ShouldGroupParagraphs(options))
            return ordered.Select(l => new OcrTextGroup([l], roles[l], 0, 1)).ToList();

        var groups = new List<OcrTextGroup>();
        foreach (var column in DetectTextColumns(ordered))
            groups.AddRange(GroupParagraphLines(column.Lines, roles, column.Index, column.Count));

        return groups;
    }

    private static IReadOnlyList<OcrTextGroup> GroupParagraphLines(
        IReadOnlyList<OcrLine> lines,
        IReadOnlyDictionary<OcrLine, OcrTextRole> roles,
        int columnIndex,
        int columnCount)
    {
        var groups = new List<OcrTextGroup>();
        var current = new List<OcrLine> { lines[0] };
        for (var i = 1; i < lines.Count; i++)
        {
            var previous = current[^1];
            var line = lines[i];
            if (roles[previous] == roles[line] && CanJoinParagraphLine(previous, line))
            {
                current.Add(line);
                continue;
            }

            groups.Add(new OcrTextGroup(current.ToList(), roles[current[0]], columnIndex, columnCount));
            current = [line];
        }

        groups.Add(new OcrTextGroup(current.ToList(), roles[current[0]], columnIndex, columnCount));
        return groups;
    }

    private static double EstimateTypicalLineHeight(IReadOnlyList<OcrLine> lines)
    {
        var heights = lines
            .Where(l => CountUsableWords(l) > 0)
            .Select(l => Math.Max(1, l.Words.Count > 0 ? l.Words.Average(w => w.Bounds.Height) : l.Bounds.Height))
            .Order()
            .ToArray();
        if (heights.Length == 0)
            return 1;

        return heights[(heights.Length - 1) / 2];
    }

    private static OcrTextRole ClassifyTextRole(OcrLine line, double typicalLineHeight)
    {
        var lineHeight = Math.Max(1, line.Words.Count > 0 ? line.Words.Average(w => w.Bounds.Height) : line.Bounds.Height);
        var wordCount = CountUsableWords(line);
        if (typicalLineHeight > 0 && lineHeight >= typicalLineHeight * 1.35 && wordCount <= 10)
            return OcrTextRole.Heading;
        if (typicalLineHeight > 0 && lineHeight <= typicalLineHeight * 0.78)
            return OcrTextRole.Caption;

        return OcrTextRole.Body;
    }

    private static string ToTextRoleName(OcrTextRole role) =>
        role switch
        {
            OcrTextRole.Heading => "heading",
            OcrTextRole.Caption => "caption",
            _ => "body",
        };

    private static IReadOnlyList<OcrTextColumn> DetectTextColumns(IReadOnlyList<OcrLine> lines)
    {
        if (lines.Count < 4)
            return [new OcrTextColumn(lines, 0, 1)];

        var averageHeight = lines.Average(l => Math.Max(1, l.Bounds.Height));
        var tolerance = Math.Max(12, averageHeight * 1.5);
        var clusters = new List<List<OcrLine>>();

        foreach (var line in lines.OrderBy(l => l.Bounds.X).ThenBy(l => l.Bounds.Y))
        {
            var match = clusters
                .Select((cluster, index) => new
                {
                    Cluster = cluster,
                    Index = index,
                    Distance = Math.Abs(cluster.Average(l => l.Bounds.X) - line.Bounds.X),
                })
                .Where(x => x.Distance <= tolerance)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            if (match is null)
                clusters.Add([line]);
            else
                clusters[match.Index].Add(line);
        }

        if (clusters.Count < 2 || clusters.Any(c => c.Count < 2))
            return [new OcrTextColumn(lines, 0, 1)];

        var sorted = clusters
            .Select(c => c.OrderBy(l => l.Bounds.Y).ThenBy(l => l.Bounds.X).ToList())
            .OrderBy(c => c.Min(l => l.Bounds.X))
            .ToList();
        for (var i = 1; i < sorted.Count; i++)
        {
            var previousRight = sorted[i - 1].Max(l => l.Bounds.X + l.Bounds.Width);
            var currentLeft = sorted[i].Min(l => l.Bounds.X);
            if (currentLeft - previousRight < Math.Max(24, averageHeight * 3))
                return [new OcrTextColumn(lines, 0, 1)];
        }

        return sorted
            .Select((columnLines, index) => new OcrTextColumn(columnLines, index, sorted.Count))
            .ToList();
    }

    private static bool ShouldGroupParagraphs(ImageToPdfConversionOptions options) =>
        string.Equals(options.LayoutMode, "structured", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(options.LayoutMode, "paragraphs", StringComparison.OrdinalIgnoreCase);

    private static bool CanJoinParagraphLine(OcrLine previous, OcrLine current)
    {
        if (CountUsableWords(previous) == 0 || CountUsableWords(current) == 0)
            return false;

        var previousHeight = Math.Max(1, previous.Bounds.Height);
        var currentHeight = Math.Max(1, current.Bounds.Height);
        var averageHeight = (previousHeight + currentHeight) / 2.0;
        var heightSimilarity = Math.Min(previousHeight, currentHeight) / (double)Math.Max(previousHeight, currentHeight);
        if (heightSimilarity < 0.65)
            return false;

        var verticalGap = current.Bounds.Y - (previous.Bounds.Y + previous.Bounds.Height);
        if (verticalGap < 0 || verticalGap > Math.Max(8, averageHeight * 0.9))
            return false;

        var leftDelta = Math.Abs(current.Bounds.X - previous.Bounds.X);
        if (leftDelta > Math.Max(8, averageHeight * 0.8))
            return false;

        var widthRatio = Math.Min(previous.Bounds.Width, current.Bounds.Width) / (double)Math.Max(previous.Bounds.Width, current.Bounds.Width);
        return widthRatio >= 0.35;
    }

    private static IReadOnlyList<OcrTableCandidate> DetectTables(
        IReadOnlyList<OcrLine> lines,
        ImageToPdfConversionOptions options)
    {
        if (!string.Equals(options.LayoutMode, "structured", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.LayoutMode, "tables", StringComparison.OrdinalIgnoreCase))
            return [];

        var result = new List<OcrTableCandidate>();
        var index = 0;
        while (index < lines.Count)
        {
            var line = lines[index];
            var columnCount = CountUsableWords(line);
            if (columnCount < 2)
            {
                index++;
                continue;
            }

            var group = new List<OcrLine> { line };
            var anchors = GetWordAnchors(line);
            var tolerance = Math.Max(12, line.Bounds.Height * 1.5);
            var next = index + 1;

            while (next < lines.Count &&
                   TryMergeTableAnchors(anchors, GetWordAnchors(lines[next]), tolerance, out var mergedAnchors))
            {
                group.Add(lines[next]);
                anchors = mergedAnchors;
                columnCount = anchors.Length;
                next++;
            }

            if (group.Count >= 2)
            {
                result.Add(new OcrTableCandidate(group, anchors, null));
                index = next;
            }
            else
            {
                index++;
            }
        }

        return result;
    }

    private static string[][] BuildTableCellData(OcrTableCandidate table)
    {
        var tolerance = EstimateTableColumnTolerance(table);
        return table.Lines
            .Select(line =>
            {
                var cells = Enumerable.Repeat(string.Empty, table.ColumnAnchors.Count).ToArray();
                foreach (var word in line.Words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderBy(w => w.Bounds.X))
                {
                    var column = FindNearestColumn(word.Bounds.X + word.Bounds.Width / 2.0, table.ColumnAnchors, tolerance);
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
                .Where(w => FindNearestColumn(w.Bounds.X + w.Bounds.Width / 2.0, table.ColumnAnchors, EstimateTableColumnTolerance(table)) == column)
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

    private static double EstimateTableColumnTolerance(OcrTableCandidate table)
    {
        var lineHeight = table.Lines.Average(l => Math.Max(1, l.Bounds.Height));
        var anchorGap = table.ColumnAnchors.Count < 2
            ? lineHeight * 2
            : table.ColumnAnchors.Zip(table.ColumnAnchors.Skip(1), (a, b) => b - a).Min();
        return Math.Max(12, Math.Min(anchorGap * 0.45, lineHeight * 2.25));
    }

    private static bool ShouldDetectShapes(ImageToPdfConversionOptions options) =>
        string.Equals(options.LayoutMode, "structured", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(options.LayoutMode, "shapes", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<OcrShapeCandidate> DetectShapes(IReadOnlyList<RuleSegment> segments)
    {
        if (segments.Count == 0)
            return [];

        var used = new HashSet<RuleSegment>();
        var rectangles = DetectRectangles(segments, used);
        var lines = segments
            .Where(s => !used.Contains(s))
            .Select(s =>
            {
                var bounds = s.Orientation == RuleOrientation.Horizontal
                    ? new OcrBoundingBox(s.X, s.Y, s.Length, 1)
                    : new OcrBoundingBox(s.X, s.Y, 1, s.Length);
                var kind = s.Orientation == RuleOrientation.Horizontal
                    ? OcrShapeKind.HorizontalLine
                    : OcrShapeKind.VerticalLine;
                return new OcrShapeCandidate(kind, bounds);
            });

        return rectangles.Concat(lines).ToList();
    }

    private static IReadOnlyList<OcrShapeCandidate> DetectRectangles(
        IReadOnlyList<RuleSegment> segments,
        HashSet<RuleSegment> used)
    {
        var rectangles = new List<OcrShapeCandidate>();
        var horizontal = segments
            .Where(s => s.Orientation == RuleOrientation.Horizontal)
            .OrderBy(s => s.Y)
            .ThenBy(s => s.X)
            .ToList();
        var vertical = segments
            .Where(s => s.Orientation == RuleOrientation.Vertical)
            .OrderBy(s => s.X)
            .ThenBy(s => s.Y)
            .ToList();

        foreach (var top in horizontal)
        {
            if (used.Contains(top))
                continue;

            foreach (var bottom in horizontal.Where(s => s.Y > top.Y + 8))
            {
                if (used.Contains(bottom))
                    continue;

                var leftX = Math.Max(top.X, bottom.X);
                var rightX = Math.Min(top.X + top.Length, bottom.X + bottom.Length);
                if (rightX - leftX < 12)
                    continue;

                foreach (var left in vertical.Where(s => !used.Contains(s) && s.X >= leftX - 1 && s.X <= rightX + 1))
                {
                    foreach (var right in vertical.Where(s => !used.Contains(s) && s.X > left.X + 8 && s.X >= leftX - 1 && s.X <= rightX + 1))
                    {
                        if (!VerticalCovers(left, top.Y, bottom.Y) ||
                            !VerticalCovers(right, top.Y, bottom.Y) ||
                            !HorizontalCovers(top, left.X, right.X) ||
                            !HorizontalCovers(bottom, left.X, right.X))
                            continue;

                        var bounds = new OcrBoundingBox(
                            left.X,
                            top.Y,
                            Math.Max(1, right.X - left.X),
                            Math.Max(1, bottom.Y - top.Y));
                        rectangles.Add(new OcrShapeCandidate(OcrShapeKind.Rectangle, bounds));
                        used.Add(top);
                        used.Add(bottom);
                        used.Add(left);
                        used.Add(right);
                        break;
                    }

                    if (used.Contains(top))
                        break;
                }

                if (used.Contains(top))
                    break;
            }
        }

        return rectangles;
    }

    private static bool HorizontalCovers(RuleSegment segment, int left, int right) =>
        segment.X <= left + 1 && segment.X + segment.Length >= right - 1;

    private static bool VerticalCovers(RuleSegment segment, int top, int bottom) =>
        segment.Y <= top + 1 && segment.Y + segment.Length >= bottom - 1;

    private static bool IsSegmentInsideAnyBounds(RuleSegment segment, IReadOnlyList<OcrBoundingBox> bounds) =>
        bounds.Any(bound => IsSegmentInsideBounds(segment, bound));

    private static bool IsSegmentInsideBounds(RuleSegment segment, OcrBoundingBox bounds)
    {
        const int tolerance = 1;
        return segment.Orientation == RuleOrientation.Horizontal
            ? segment.Y >= bounds.Y - tolerance &&
              segment.Y <= bounds.Y + bounds.Height + tolerance &&
              segment.X >= bounds.X - tolerance &&
              segment.X + segment.Length <= bounds.X + bounds.Width + tolerance
            : segment.X >= bounds.X - tolerance &&
              segment.X <= bounds.X + bounds.Width + tolerance &&
              segment.Y >= bounds.Y - tolerance &&
              segment.Y + segment.Length <= bounds.Y + bounds.Height + tolerance;
    }

    private static int CountUsableWords(OcrLine line) =>
        line.Words.Count(w => !string.IsNullOrWhiteSpace(w.Text));

    private static double[] GetWordAnchors(OcrLine line) =>
        line.Words
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderBy(w => w.Bounds.X)
            .Select(w => w.Bounds.X + w.Bounds.Width / 2.0)
            .ToArray();

    private static bool TryMergeTableAnchors(
        double[] expected,
        double[] actual,
        double tolerance,
        out double[] merged)
    {
        merged = expected;
        if (expected.Length < 2 || actual.Length == 0 || actual.Length > expected.Length + 1)
            return false;

        if (actual.Length >= 2 && expected.Length == actual.Length)
        {
            var directMatches = expected
                .Zip(actual, (e, a) => Math.Abs(e - a) <= tolerance)
                .Count(matches => matches);
            if (directMatches == expected.Length)
            {
                merged = expected.Zip(actual, (e, a) => (e + a) / 2.0).ToArray();
                return true;
            }
        }

        var assignments = new HashSet<int>();
        foreach (var anchor in actual)
        {
            var column = FindNearestColumn(anchor, expected, tolerance);
            if (column < 0 || !assignments.Add(column))
                return false;
        }

        if (assignments.Count < Math.Min(2, expected.Length))
            return false;

        return true;
    }

    private static int FindNearestColumn(double anchor, IReadOnlyList<double> columns, double tolerance)
    {
        var bestIndex = -1;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < columns.Count; i++)
        {
            var distance = Math.Abs(columns[i] - anchor);
            if (distance < bestDistance)
            {
                bestIndex = i;
                bestDistance = distance;
            }
        }

        return bestDistance <= tolerance ? bestIndex : -1;
    }

    private static OcrBoundingBox UnionBounds(IEnumerable<OcrBoundingBox> boxes)
    {
        var list = boxes.ToList();
        if (list.Count == 0)
            return new OcrBoundingBox(0, 0, 1, 1);

        var left = list.Min(b => b.X);
        var top = list.Min(b => b.Y);
        var right = list.Max(b => b.X + b.Width);
        var bottom = list.Max(b => b.Y + b.Height);
        return new OcrBoundingBox(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static string EstimateTextColor(SKBitmap bitmap, OcrBoundingBox bounds)
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
                if (IsLikelyTextPixel(color))
                    samples.Add(color);
            }
        }

        if (samples.Count < 3)
            return "#111827";

        var red = Median(samples.Select(c => c.Red));
        var green = Median(samples.Select(c => c.Green));
        var blue = Median(samples.Select(c => c.Blue));
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static bool IsLikelyTextPixel(SKColor color)
    {
        if (color.Alpha < 180)
            return false;

        var luma = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;
        var saturation = (Math.Max(color.Red, Math.Max(color.Green, color.Blue)) -
                          Math.Min(color.Red, Math.Min(color.Green, color.Blue))) / 255.0;

        return luma < 130 || (luma < 210 && saturation > 0.35);
    }

    private static byte Median(IEnumerable<byte> values)
    {
        var sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
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

    private static OcrBoundingBox? FindRuleBounds(OcrTableCandidate table, IReadOnlyList<RuleSegment> segments)
    {
        if (segments.Count == 0)
            return null;

        var wordBounds = UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds)));
        var margin = Math.Max(20, table.Lines.Average(l => l.Bounds.Height) * 2.5);

        var horizontal = segments
            .Where(s => s.Orientation == RuleOrientation.Horizontal &&
                        s.X <= wordBounds.X + wordBounds.Width + margin &&
                        s.X + s.Length >= wordBounds.X - margin &&
                        s.Y >= wordBounds.Y - margin &&
                        s.Y <= wordBounds.Y + wordBounds.Height + margin)
            .ToList();
        var vertical = segments
            .Where(s => s.Orientation == RuleOrientation.Vertical &&
                        s.Y <= wordBounds.Y + wordBounds.Height + margin &&
                        s.Y + s.Length >= wordBounds.Y - margin &&
                        s.X >= wordBounds.X - margin &&
                        s.X <= wordBounds.X + wordBounds.Width + margin)
            .ToList();

        if (horizontal.Count < 2)
            return null;

        var top = horizontal.Min(s => s.Y);
        var bottom = horizontal.Max(s => s.Y);
        var horizontalLeft = horizontal.Min(s => s.X);
        var horizontalRight = horizontal.Max(s => s.X + s.Length);
        var left = vertical.Count >= 2 ? vertical.Min(s => s.X) : horizontalLeft;
        var right = vertical.Count >= 2 ? vertical.Max(s => s.X) : horizontalRight;
        if (right <= left || bottom <= top)
            return null;

        if (wordBounds.X < left - 1 || wordBounds.Y < top - 1 ||
            wordBounds.X + wordBounds.Width > right + 1 ||
            wordBounds.Y + wordBounds.Height > bottom + 1)
            return null;

        if (!HasEnoughHorizontalTableCoverage(horizontal, left, right, top, bottom))
            return null;

        return new OcrBoundingBox(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static bool HasEnoughHorizontalTableCoverage(
        IReadOnlyList<RuleSegment> horizontal,
        int left,
        int right,
        int top,
        int bottom)
    {
        var requiredWidth = Math.Max(1, right - left);
        var topCoverage = MeasureHorizontalCoverage(horizontal.Where(s => Math.Abs(s.Y - top) <= 1), left, right);
        var bottomCoverage = MeasureHorizontalCoverage(horizontal.Where(s => Math.Abs(s.Y - bottom) <= 1), left, right);
        return topCoverage >= requiredWidth * 0.55 && bottomCoverage >= requiredWidth * 0.55;
    }

    private static int MeasureHorizontalCoverage(IEnumerable<RuleSegment> segments, int left, int right)
    {
        var intervals = segments
            .Select(s => (Start: Math.Max(left, s.X), End: Math.Min(right, s.X + s.Length)))
            .Where(i => i.End > i.Start)
            .OrderBy(i => i.Start)
            .ToList();
        if (intervals.Count == 0)
            return 0;

        var coverage = 0;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;
        foreach (var interval in intervals.Skip(1))
        {
            if (interval.Start <= currentEnd + 2)
            {
                currentEnd = Math.Max(currentEnd, interval.End);
                continue;
            }

            coverage += currentEnd - currentStart;
            currentStart = interval.Start;
            currentEnd = interval.End;
        }

        coverage += currentEnd - currentStart;
        return coverage;
    }

    private static IReadOnlyList<RuleSegment> DetectRuleSegments(SKBitmap bitmap)
    {
        var segments = new List<RuleSegment>();
        var minHorizontalRun = Math.Max(16, bitmap.Width / 8);
        var minVerticalRun = Math.Max(16, bitmap.Height / 8);

        for (var y = 0; y < bitmap.Height; y++)
        {
            var runStart = -1;
            for (var x = 0; x <= bitmap.Width; x++)
            {
                var dark = x < bitmap.Width && IsDarkRulePixel(bitmap.GetPixel(x, y));
                if (dark && runStart < 0)
                    runStart = x;
                else if (!dark && runStart >= 0)
                {
                    var length = x - runStart;
                    if (length >= minHorizontalRun)
                        segments.Add(new RuleSegment(RuleOrientation.Horizontal, runStart, y, length));
                    runStart = -1;
                }
            }
        }

        for (var x = 0; x < bitmap.Width; x++)
        {
            var runStart = -1;
            for (var y = 0; y <= bitmap.Height; y++)
            {
                var dark = y < bitmap.Height && IsDarkRulePixel(bitmap.GetPixel(x, y));
                if (dark && runStart < 0)
                    runStart = y;
                else if (!dark && runStart >= 0)
                {
                    var length = y - runStart;
                    if (length >= minVerticalRun)
                        segments.Add(new RuleSegment(RuleOrientation.Vertical, x, runStart, length));
                    runStart = -1;
                }
            }
        }

        return segments;
    }

    private static bool IsDarkRulePixel(SKColor color)
    {
        if (color.Alpha < 180)
            return false;

        var luma = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;
        return luma < 80;
    }

    private enum RuleOrientation
    {
        Horizontal,
        Vertical,
    }

    private sealed record RuleSegment(RuleOrientation Orientation, int X, int Y, int Length);

    private enum OcrShapeKind
    {
        HorizontalLine,
        VerticalLine,
        Rectangle,
    }

    private sealed record OcrShapeCandidate(OcrShapeKind Kind, OcrBoundingBox Bounds);

    private enum OcrTextRole
    {
        Body,
        Heading,
        Caption,
    }

    private sealed record OcrTextGroup(
        IReadOnlyList<OcrLine> Lines,
        OcrTextRole Role,
        int ColumnIndex,
        int ColumnCount);

    private sealed record OcrTextColumn(IReadOnlyList<OcrLine> Lines, int Index, int Count);

    private sealed record OcrTextRun(
        string Text,
        double X,
        double Y,
        double Width,
        double Height,
        double FontSize,
        string Color,
        double Confidence,
        OcrBoundingBox SourceBounds);

    private sealed record OcrTableCandidate(
        IReadOnlyList<OcrLine> Lines,
        IReadOnlyList<double> ColumnAnchors,
        OcrBoundingBox? RuleBounds);

    private static ImagePlacement ResolveImagePlacement(
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

    private sealed record ImagePlacement(double X, double Y, double Width, double Height, double Scale);

    private static IReadOnlyList<string> BuildWarnings(
        double dpiX,
        double dpiY,
        IReadOnlyList<OcrWord> words,
        int lowConfidenceWords,
        ImageToPdfConversionOptions options)
    {
        var warnings = new List<string>();
        if (dpiX < 150 || dpiY < 150)
            warnings.Add("Input DPI is low; OCR accuracy may be reduced.");
        if (words.Count == 0)
            warnings.Add("No OCR words were detected.");
        if (lowConfidenceWords > 0)
            warnings.Add($"{lowConfidenceWords} OCR words are below the configured confidence threshold.");
        if (!options.IncludeBackgroundImage)
            warnings.Add("Background image layer is disabled; visual fidelity depends entirely on reconstructed elements.");
        return warnings;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static (string DataUri, byte[] EncodedBytes) EncodeImage(SKBitmap bitmap)
    {
        var bytes = EncodeImageBytes(bitmap);
        return ($"data:image/png;base64,{Convert.ToBase64String(bytes)}", bytes);
    }

    private static byte[] EncodeImageBytes(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKBitmap PreprocessForOcr(
        SKBitmap source,
        ImageToPdfConversionOptions options,
        out IReadOnlyList<string> steps)
    {
        var applied = new List<string>();
        if (!options.EnablePreprocessing)
        {
            steps = applied;
            return source.Copy();
        }

        var grayscale = options.PreprocessGrayscale;
        var contrast = options.PreprocessContrast;
        var binarize = options.PreprocessBinarize;
        if (!grayscale && !contrast && !binarize)
        {
            steps = applied;
            return source.Copy();
        }

        var bitmap = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var contrastFactor = contrast ? 1.25 : 1.0;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                var luma = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;
                var value = contrast
                    ? Math.Clamp((luma - 128) * contrastFactor + 128, 0, 255)
                    : luma;

                if (binarize)
                    value = value >= 160 ? 255 : 0;

                var channel = (byte)Math.Round(value);
                var output = grayscale || contrast || binarize
                    ? new SKColor(channel, channel, channel, color.Alpha)
                    : color;
                bitmap.SetPixel(x, y, output);
            }
        }

        if (grayscale)
            applied.Add("grayscale");
        if (contrast)
            applied.Add("contrast");
        if (binarize)
            applied.Add("binarize");

        steps = applied;
        return bitmap;
    }

    private static (double? X, double? Y) ReadImageDpi(byte[] raw, SKEncodedOrigin origin)
    {
        var dpi = ReadPngDpi(raw) ?? ReadJpegDpi(raw);
        if (dpi is null)
            return (null, null);

        return SwapsAxes(origin) ? (dpi.Value.Y, dpi.Value.X) : dpi.Value;
    }

    private static (double X, double Y)? ReadPngDpi(byte[] raw)
    {
        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (raw.Length < 33 || !raw.AsSpan(0, 8).SequenceEqual(pngSignature))
            return null;

        var offset = 8;
        while (offset + 12 <= raw.Length)
        {
            var length = ReadUInt32BigEndian(raw, offset);
            if (length > int.MaxValue || offset + 12 + length > raw.Length)
                return null;

            var typeOffset = offset + 4;
            if (raw[typeOffset] == (byte)'p' &&
                raw[typeOffset + 1] == (byte)'H' &&
                raw[typeOffset + 2] == (byte)'Y' &&
                raw[typeOffset + 3] == (byte)'s' &&
                length >= 9)
            {
                var dataOffset = offset + 8;
                if (raw[dataOffset + 8] != 1)
                    return null;

                var xPixelsPerMeter = ReadUInt32BigEndian(raw, dataOffset);
                var yPixelsPerMeter = ReadUInt32BigEndian(raw, dataOffset + 4);
                var x = NormalizeDpi(xPixelsPerMeter / 39.37007874015748);
                var y = NormalizeDpi(yPixelsPerMeter / 39.37007874015748);
                return x is not null && y is not null ? (x.Value, y.Value) : null;
            }

            offset += 12 + (int)length;
        }

        return null;
    }

    private static (double X, double Y)? ReadJpegDpi(byte[] raw)
    {
        if (raw.Length < 4 || raw[0] != 0xFF || raw[1] != 0xD8)
            return null;

        var offset = 2;
        while (offset + 4 <= raw.Length)
        {
            while (offset < raw.Length && raw[offset] != 0xFF)
                offset++;
            while (offset < raw.Length && raw[offset] == 0xFF)
                offset++;
            if (offset >= raw.Length)
                return null;

            var marker = raw[offset++];
            if (marker is 0xD9 or 0xDA)
                return null;
            if (marker is >= 0xD0 and <= 0xD7)
                continue;
            if (offset + 2 > raw.Length)
                return null;

            var length = ReadUInt16BigEndian(raw, offset);
            if (length < 2 || offset + length > raw.Length)
                return null;

            var dataOffset = offset + 2;
            var dataLength = length - 2;
            if (marker == 0xE0 && dataLength >= 14 &&
                raw[dataOffset] == (byte)'J' &&
                raw[dataOffset + 1] == (byte)'F' &&
                raw[dataOffset + 2] == (byte)'I' &&
                raw[dataOffset + 3] == (byte)'F' &&
                raw[dataOffset + 4] == 0)
            {
                var units = raw[dataOffset + 7];
                var xDensity = ReadUInt16BigEndian(raw, dataOffset + 8);
                var yDensity = ReadUInt16BigEndian(raw, dataOffset + 10);
                if (xDensity == 0 || yDensity == 0)
                    return null;

                var multiplier = units switch
                {
                    1 => 1.0,
                    2 => 2.54,
                    _ => 0,
                };
                if (multiplier <= 0)
                    return null;

                var x = NormalizeDpi(xDensity * multiplier);
                var y = NormalizeDpi(yDensity * multiplier);
                return x is not null && y is not null ? (x.Value, y.Value) : null;
            }

            offset += length;
        }

        return null;
    }

    private static double? NormalizeDpi(double? dpi) =>
        dpi is > 0 and <= 2400 ? dpi : null;

    private static uint ReadUInt32BigEndian(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) |
        ((uint)bytes[offset + 1] << 16) |
        ((uint)bytes[offset + 2] << 8) |
        bytes[offset + 3];

    private static ushort ReadUInt16BigEndian(byte[] bytes, int offset) =>
        (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

    private static SKBitmap ApplyOrientation(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return src.Copy();

        var swap = origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

        var dstW = swap ? src.Height : src.Width;
        var dstH = swap ? src.Width : src.Height;
        var dst = new SKBitmap(dstW, dstH, src.ColorType, src.AlphaType);
        using var canvas = new SKCanvas(dst);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(dstW, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(dstW, dstH);
                canvas.Scale(-1, -1);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, dstH);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(dstW, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(dstW, dstH);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, dstH);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(src, 0, 0);
        canvas.Flush();
        return dst;
    }

    private static bool SwapsAxes(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;
}
