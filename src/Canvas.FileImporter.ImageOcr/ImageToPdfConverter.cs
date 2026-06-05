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
            elements.Add(BuildTableElement(table, placement));

        foreach (var line in lines)
        {
            if (tableLines.Contains(line))
                continue;

            var x = placement.X + line.Bounds.X * placement.Scale;
            var y = placement.Y + line.Bounds.Y * placement.Scale;
            var width = Math.Max(1, line.Bounds.Width * placement.Scale);
            var height = Math.Max(1, line.Bounds.Height * placement.Scale);
            var fontSize = Math.Clamp(height * 0.78, 6, 72);

            elements.Add(new ElementDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "text",
                Name = "OCR text",
                X = Math.Round(x, 2),
                Y = Math.Round(y, 2),
                Width = Math.Round(width, 2),
                Height = Math.Round(height, 2),
                Content = line.Text,
                Style = new Dictionary<string, object>
                {
                    ["fontSize"] = Math.Round(fontSize, 2),
                    ["color"] = "#111827",
                    ["imageOcrConfidence"] = line.Confidence,
                    ["sourceBoundsPx"] = $"{line.Bounds.X},{line.Bounds.Y},{line.Bounds.Width},{line.Bounds.Height}",
                },
            });
        }

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

    private static ElementDto BuildTableElement(OcrTableCandidate table, ImagePlacement placement)
    {
        var wordBounds = UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds)));
        var bounds = table.RuleBounds ?? wordBounds;
        var paddingPx = Math.Max(2, table.Lines.Average(l => l.Bounds.Height) * 0.35);
        var useRuleBounds = table.RuleBounds is not null;
        var x = placement.X + Math.Max(0, bounds.X - (useRuleBounds ? 0 : paddingPx)) * placement.Scale;
        var y = placement.Y + Math.Max(0, bounds.Y - (useRuleBounds ? 0 : paddingPx)) * placement.Scale;
        var width = (bounds.Width + (useRuleBounds ? 0 : paddingPx * 2)) * placement.Scale;
        var height = (bounds.Height + (useRuleBounds ? 0 : paddingPx * 2)) * placement.Scale;

        var cellData = table.Lines
            .Select(line => line.Words
                .OrderBy(w => w.Bounds.X)
                .Select(w => w.Text)
                .ToArray())
            .ToArray();
        var columnWidths = Enumerable.Range(0, table.ColumnCount)
            .Select(column => table.Lines
                .Select(line => line.Words.OrderBy(w => w.Bounds.X).ElementAt(column).Bounds.Width)
                .Average())
            .ToArray();

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
                ["columns"] = table.ColumnCount,
                ["fontSize"] = Math.Round(Math.Clamp(table.Lines.Average(l => l.Bounds.Height) * placement.Scale * 0.68, 6, 18), 2),
                ["color"] = "#111827",
                ["borderColor"] = "#9ca3af",
                ["borderWidth"] = 0.75,
                ["cellPadding"] = 3,
                ["imageOcrRole"] = "table",
                ["imageOcrRuleBounded"] = useRuleBounds,
                ["sourceBoundsPx"] = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}",
            },
        };
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
                   CountUsableWords(lines[next]) == columnCount &&
                   HasSimilarColumns(anchors, GetWordAnchors(lines[next]), tolerance))
            {
                group.Add(lines[next]);
                next++;
            }

            if (group.Count >= 2)
            {
                result.Add(new OcrTableCandidate(group, columnCount, null));
                index = next;
            }
            else
            {
                index++;
            }
        }

        return result;
    }

    private static int CountUsableWords(OcrLine line) =>
        line.Words.Count(w => !string.IsNullOrWhiteSpace(w.Text));

    private static double[] GetWordAnchors(OcrLine line) =>
        line.Words
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderBy(w => w.Bounds.X)
            .Select(w => w.Bounds.X + w.Bounds.Width / 2.0)
            .ToArray();

    private static bool HasSimilarColumns(double[] expected, double[] actual, double tolerance)
    {
        if (expected.Length != actual.Length)
            return false;

        for (var i = 0; i < expected.Length; i++)
        {
            if (Math.Abs(expected[i] - actual[i]) > tolerance)
                return false;
        }

        return true;
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

        if (horizontal.Count < 2 || vertical.Count < 2)
            return null;

        var left = vertical.Min(s => s.X);
        var right = vertical.Max(s => s.X);
        var top = horizontal.Min(s => s.Y);
        var bottom = horizontal.Max(s => s.Y);
        if (right <= left || bottom <= top)
            return null;

        if (wordBounds.X < left || wordBounds.Y < top ||
            wordBounds.X + wordBounds.Width > right ||
            wordBounds.Y + wordBounds.Height > bottom)
            return null;

        return new OcrBoundingBox(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
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

    private sealed record OcrTableCandidate(
        IReadOnlyList<OcrLine> Lines,
        int ColumnCount,
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
