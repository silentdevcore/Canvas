using PXA.Core.Contracts;
using SkiaSharp;
using System.Diagnostics;

namespace PXA.FileImporter.ImageOcr;

// Orchestrator for the image-OCR pipeline. ConvertAsync decodes the image,
// applies orientation/DPI/preprocessing, then drives the four pipeline stages:
//   1. OcrTextExtractor       — OCR text extraction (raw words/lines/blocks).
//   2. VisualElementDetector  — pixel-only visual candidates (rules, shapes, ...).
//   3. OcrVisualFusionEngine  — fuse OCR text with visual candidates (tables,
//                               fields, signatures, standalone text groups).
//   4. PxaElementBuilder   — build PXA ElementDto objects.
public sealed class ImageToPdfConverter
{
    private const double DefaultDpi = 300;
    private const long MaxOcrImagePixels = 4_000_000;
    // Cheap rule-based detection (rule scans for checkboxes, fields, signatures, lines and
    // rectangles) is O(pixels) and runs up to this size.
    private const long RuleShapeMaxPixels = 6_000_000;
    // The whole-image flood-fill detectors (filled rectangles, circles/ellipses, image
    // regions) are super-linear on text-dense pages (~43 s at 2.3 MP), so they only run on
    // small images. Larger images still get OCR text, tables and rule-based shapes.
    private const long FloodFillMaxPixels = 1_000_000;

    private readonly OcrTextExtractor _ocrTextExtractor;

    public ImageToPdfConverter(IOcrEngine ocrEngine)
    {
        _ocrTextExtractor = new OcrTextExtractor(ocrEngine);
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
        using var preprocessedBitmap = PreprocessForOcr(bitmap, options, out var preprocessingSteps);
        using var ocrBitmap = ResizeForOcr(preprocessedBitmap, preprocessingSteps, out var effectivePreprocessingSteps, out var ocrScaleFactor);
        var encodedForOcr = effectivePreprocessingSteps.Count == 0
            ? originalEncoded
            : EncodeImageBytes(ocrBitmap);

        var dpiX = NormalizeDpi(options.SourceDpiX) ?? metadataDpi.X ?? DefaultDpi;
        var dpiY = NormalizeDpi(options.SourceDpiY) ?? metadataDpi.Y ?? DefaultDpi;
        var (pageWidth, pageHeight) = ResolvePageSize(bitmap.Width, bitmap.Height, dpiX, dpiY, options);
        // Text-only / text-background: shrink the page to the scan's own aspect ratio within the
        // selected page size, so off-aspect scans aren't letterboxed (no centering margin) while
        // the page stays the expected size. For an A4-aspect scan this is effectively a no-op.
        if (IsAspectFitLayout(options.LayoutMode) && bitmap.Width > 0 && bitmap.Height > 0)
        {
            var fit = Math.Min(pageWidth / bitmap.Width, pageHeight / bitmap.Height);
            pageWidth = bitmap.Width * fit;
            pageHeight = bitmap.Height * fit;
        }

        OcrTextDocument ocrTextDocument;
        try
        {
            ocrTextDocument = await _ocrTextExtractor.ExtractAsync(
                encodedForOcr,
                ocrBitmap.Width,
                ocrBitmap.Height,
                bitmap.Width,
                bitmap.Height,
                options,
                effectivePreprocessingSteps,
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

        var ocrPages = ocrTextDocument.Pages;
        var designResult = BuildDesign(sourceName, dataUri, bitmap, pageWidth, pageHeight, ocrPages, options);
        stopwatch.Stop();
        var memoryAfter = GC.GetTotalMemory(false);

        var words = ocrTextDocument.Words;
        var lines = ocrTextDocument.Lines;
        var lowConfidenceWords = words.Count(w => w.Confidence < options.LowConfidenceThreshold);
        var warnings = BuildWarnings(dpiX, dpiY, (long)bitmap.Width * bitmap.Height, ocrScaleFactor, words, lowConfidenceWords, options);

        return new ImageToPdfConversionResult
        {
            Design = designResult.Design,
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
                PreprocessingApplied = effectivePreprocessingSteps.Count > 0,
                PreprocessingScaleFactor = Math.Round(ocrScaleFactor, 4),
                PreprocessingSteps = effectivePreprocessingSteps,
                PageCount = ocrPages.Count,
                OcrEngine = ocrTextDocument.Metadata.EngineName,
                OcrEngineVersion = ocrTextDocument.Metadata.EngineVersion,
                Languages = ocrTextDocument.Metadata.Language,
                WordCount = words.Count,
                LineCount = lines.Count,
                AverageConfidence = words.Count == 0 ? 0 : Math.Round(words.Average(w => w.Confidence), 4),
                LowConfidenceWordCount = lowConfidenceWords,
                RuntimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
                MemoryDeltaBytes = memoryAfter - memoryBefore,
                Layout = designResult.Diagnostics,
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

    // Orchestrates stages 2-4: visual detection, OCR/visual fusion, and element
    // building. The interleaving (visual detection consumes OCR-derived exclusion
    // zones, fusion consumes visual rule/shape candidates) is sequenced here.
    private static DesignBuildResult BuildDesign(
        string name,
        string dataUri,
        SKBitmap bitmap,
        double pageWidth,
        double pageHeight,
        IReadOnlyList<OcrPage> ocrPages,
        ImageToPdfConversionOptions options)
    {
        var elements = new List<ElementDto>();
        var placement = PxaElementBuilder.ResolveImagePlacement(bitmap.Width, bitmap.Height, pageWidth, pageHeight);
        // Snapshot pixels once; the detection stages read this instead of SKBitmap.GetPixel.
        var pixels = new OcrPixels(bitmap);
        var pixelCount = (long)bitmap.Width * bitmap.Height;
        var lines = ocrPages
            .SelectMany(p => p.Blocks)
            .SelectMany(b => b.Lines)
            .Where(l => !string.IsNullOrWhiteSpace(l.Text))
            .OrderBy(l => l.Bounds.Y)
            .ThenBy(l => l.Bounds.X)
            .ToList();

        // Document-level baseline text height (median word height, falling back to median line
        // height). Text sizing is clamped against this so a single outlier-tall OCR box
        // (overlapping/garbled source text, tall cell boxes) cannot blow a word up to the cap.
        var baselineHeightPx = EstimateBaselineTextHeight(lines);

        // Text-only mode: reconstruct only the text (size/position/color), with no background
        // image and no table/shape/field/signature detection. One element per line (no per-word
        // run splitting) so words stay together. Returns before any visual-detection stages run.
        if (string.Equals(options.LayoutMode, "text-only", StringComparison.OrdinalIgnoreCase))
        {
            var toTables = OcrVisualFusionEngine.DetectColumnAlignedTables(lines);
            var toTableLines = toTables.SelectMany(t => t.Lines).ToHashSet();
            foreach (var table in toTables)
                elements.Add(PxaElementBuilder.BuildTableElement(table, placement, pixels));

            var textOnlyGroups = OcrVisualFusionEngine.BuildTextGroups(
                lines.Where(l => !toTableLines.Contains(l)).ToList(), options);
            foreach (var textGroup in textOnlyGroups)
                elements.AddRange(PxaElementBuilder.BuildTextElements(textGroup, placement, pixels, baselineHeightPx, splitRuns: false));

            return new DesignBuildResult(
                BuildDesignDto(name, pageWidth, pageHeight, elements),
                BuildLayoutDiagnostics([], []));
        }

        // Text-background mode: same positioned text as text-only, plus large colored background
        // blocks (header bars, pills, cards, header-row shading) reconstructed as colored
        // rectangles behind the text. No tables/shapes/checkboxes and no original-image layer.
        if (string.Equals(options.LayoutMode, "text-background", StringComparison.OrdinalIgnoreCase))
        {
            // Detect fills on a downscaled copy (keeps flood-fill fast on large scans), then map
            // the bounds back to source coordinates.
            var (detectionPixels, detectionScale, ownedBitmap) = BuildDetectionPixels(bitmap, options.DetectionMaxPixels);
            try
            {
                var fills = VisualElementDetector.DetectBackgroundFills(detectionPixels, options)
                    .Select(f => f with { Bounds = ScaleBounds(f.Bounds, 1.0 / detectionScale) })
                    .OrderByDescending(f => (long)f.Bounds.Width * f.Bounds.Height)
                    .ToList();
                foreach (var fill in fills)
                    elements.Add(PxaElementBuilder.BuildShapeElement(fill, placement));
            }
            finally
            {
                ownedBitmap?.Dispose();
            }

            // Reconstruct borderless, column-aligned tables (e.g. invoice line-item tables with a
            // light header and no cell borders) from text alignment, then keep the consumed lines
            // out of the loose text groups.
            var bgTables = OcrVisualFusionEngine.DetectColumnAlignedTables(lines);
            var bgTableLines = bgTables.SelectMany(t => t.Lines).ToHashSet();
            foreach (var table in bgTables)
                elements.Add(PxaElementBuilder.BuildTableElement(table, placement, pixels));

            var bgTextGroups = OcrVisualFusionEngine.BuildTextGroups(
                lines.Where(l => !bgTableLines.Contains(l)).ToList(), options);
            foreach (var textGroup in bgTextGroups)
                elements.AddRange(PxaElementBuilder.BuildTextElements(textGroup, placement, pixels, baselineHeightPx, splitRuns: false));

            return new DesignBuildResult(
                BuildDesignDto(name, pageWidth, pageHeight, elements),
                BuildLayoutDiagnostics([], []));
        }

        // Rule-based detection (checkboxes/fields/signatures/lines/rectangles) is cheap and
        // runs up to RuleShapeMaxPixels; the expensive flood-fill detectors are limited to
        // FloodFillMaxPixels.
        var detectShapes = ShouldDetectShapes(options) && pixelCount <= RuleShapeMaxPixels;
        var detectFloodShapes = ShouldDetectShapes(options) && pixelCount <= FloodFillMaxPixels;

        var detectedTables = OcrVisualFusionEngine.DetectTables(lines, options).ToList();
        var tableSearchBounds = detectedTables
            .Select(GetTableWordBounds)
            .Select(b => OcrLayoutHelpers.ExpandBounds(b, Math.Max(24, (int)Math.Round(Math.Max(1, b.Height) * 1.5))))
            .ToList();

        // Stage 2: rule segment detection from pixels. Global when shape detection is on;
        // otherwise focused on table regions so tables can still be rule-validated.
        var ruleSegments = detectShapes
            ? VisualElementDetector.DetectRuleSegments(pixels, options.RuleContrastThreshold)
            : tableSearchBounds.Count > 0
                ? VisualElementDetector.DetectRuleSegments(pixels, tableSearchBounds, options.RuleContrastThreshold)
                : [];

        // Stage 3: enrich text tables with visual rule/background bounds, then keep only
        // tables backed by actual visible lines or shaded cells. Plain aligned text with no
        // such evidence is emitted as normal text rather than forced into a table.
        var enrichedTables = detectedTables
            .Select(t =>
            {
                var ruleMatch = OcrVisualFusionEngine.FindRuleBounds(t, ruleSegments);
                return t with
                {
                    RuleBounds = ruleMatch.Bounds,
                    BackgroundBounds = OcrVisualFusionEngine.FindTableBackgroundBounds(t, pixels, options),
                    RuleRejectionReason = ruleMatch.RejectionReason,
                };
            })
            .ToList();
        var tableCandidates = enrichedTables
            .Where(t => t.RuleBounds is not null || t.BackgroundBounds is not null)
            .ToList();
        var tableLines = tableCandidates
            .SelectMany(t => t.Lines)
            .ToHashSet();
        var tableVisualBounds = tableCandidates
            .Select(t => t.RuleBounds ?? t.BackgroundBounds)
            .Where(b => b is not null)
            .Cast<OcrBoundingBox>()
            .ToList();
        var textPixelBounds = lines
            .Where(l => !tableLines.Contains(l))
            .Select(l => OcrLayoutHelpers.ExpandBounds(l.Bounds, 1))
            .ToList();

        // Stage 2: checkboxes from pixels (excluding tables/text).
        var checkboxCandidates = detectShapes
            ? VisualElementDetector.DetectCheckboxes(pixels, [.. tableVisualBounds, .. textPixelBounds])
            : [];
        var checkboxBounds = checkboxCandidates
            .Select(c => c.Bounds)
            .ToList();

        // Stage 2: rule shapes (rectangles + lines) outside tables/text.
        var shapeSegments = detectShapes
            ? ruleSegments
                .Where(s => !OcrLayoutHelpers.IsSegmentInsideAnyBounds(s, tableVisualBounds))
                .Where(s => !OcrLayoutHelpers.IsSegmentInsideAnyBounds(s, textPixelBounds))
                .ToList()
            : [];
        var shapeCandidates = VisualElementDetector.DetectShapes(shapeSegments)
            .Where(s => !OcrLayoutHelpers.IsBoundsInsideAnyBounds(s.Bounds, checkboxBounds))
            .ToList();

        // Stage 3: rectangles + nearby labels become fields.
        var fieldCandidates = detectShapes
            ? OcrVisualFusionEngine.DetectFields(shapeCandidates, pixels, lines.Where(l => !tableLines.Contains(l)).ToList())
            : [];
        var fieldBounds = fieldCandidates.Select(f => f.Bounds).ToList();
        var fieldLabelLines = fieldCandidates.Select(f => f.LabelLine).ToHashSet();
        shapeCandidates = shapeCandidates
            .Where(s => !OcrLayoutHelpers.IsBoundsInsideAnyBounds(s.Bounds, fieldBounds))
            .ToList();

        // Stage 3: horizontal lines + nearby labels become signatures.
        var signatureCandidates = detectShapes
            ? OcrVisualFusionEngine.DetectSignatures(shapeCandidates, lines.Where(l => !tableLines.Contains(l) && !fieldLabelLines.Contains(l)).ToList())
            : [];
        var signatureBounds = signatureCandidates.Select(s => s.Bounds).ToList();
        var signatureLabelLines = signatureCandidates.Select(s => s.LabelLine).ToHashSet();
        shapeCandidates = shapeCandidates
            .Where(s => !OcrLayoutHelpers.IsBoundsInsideAnyBounds(s.Bounds, signatureBounds))
            .ToList();

        // Stage 2: filled rectangles, circles, image regions (excluding prior detections).
        var filledRectangleCandidates = detectFloodShapes
            ? VisualElementDetector.DetectFilledRectangles(pixels, [.. tableVisualBounds, .. checkboxBounds, .. fieldBounds, .. signatureBounds])
            : [];
        var filledRectangleBounds = filledRectangleCandidates.Select(s => s.Bounds).ToList();
        var circleCandidates = detectFloodShapes
            ? VisualElementDetector.DetectCirclesAndEllipses(pixels, [.. tableVisualBounds, .. checkboxBounds, .. fieldBounds, .. signatureBounds, .. filledRectangleBounds])
            : [];
        var circleBounds = circleCandidates.Select(s => s.Bounds).ToList();
        shapeCandidates = shapeCandidates
            .Where(s => !OcrLayoutHelpers.IsBoundsInsideAnyBounds(s.Bounds, filledRectangleBounds) && !OcrLayoutHelpers.IsBoundsInsideAnyBounds(s.Bounds, circleBounds))
            .ToList();
        var imageRegionExcludedBounds = OcrVisualFusionEngine.BuildImageRegionExcludedBounds(
            lines,
            tableCandidates,
            checkboxBounds,
            fieldBounds,
            signatureBounds,
            filledRectangleBounds,
            circleBounds,
            shapeCandidates.Select(s => s.Bounds).ToList());
        var imageRegionCandidates = detectFloodShapes
            ? VisualElementDetector.DetectImageRegions(pixels, imageRegionExcludedBounds)
            : [];

        // Stage 4: build elements in priority order (table, checkbox, signature,
        // field, image region, shape, text).
        if (options.IncludeBackgroundImage)
            elements.Add(PxaElementBuilder.BuildBackgroundImageElement(dataUri, placement));

        foreach (var table in tableCandidates)
            elements.Add(PxaElementBuilder.BuildTableElement(table, placement, pixels));

        foreach (var checkbox in checkboxCandidates)
            elements.Add(PxaElementBuilder.BuildCheckboxElement(checkbox, placement));

        foreach (var field in fieldCandidates)
            elements.Add(PxaElementBuilder.BuildFieldElement(field, placement));

        foreach (var signature in signatureCandidates)
            elements.Add(PxaElementBuilder.BuildSignatureElement(signature, placement));

        foreach (var filledRectangle in filledRectangleCandidates)
            elements.Add(PxaElementBuilder.BuildShapeElement(filledRectangle, placement));

        foreach (var circle in circleCandidates)
            elements.Add(PxaElementBuilder.BuildShapeElement(circle, placement));

        foreach (var shape in shapeCandidates)
            elements.Add(PxaElementBuilder.BuildShapeElement(shape, placement));

        foreach (var imageRegion in imageRegionCandidates)
            elements.Add(PxaElementBuilder.BuildImageRegionElement(imageRegion, placement, bitmap));

        var textGroups = OcrVisualFusionEngine.BuildTextGroups(lines.Where(l => !tableLines.Contains(l) && !fieldLabelLines.Contains(l) && !signatureLabelLines.Contains(l)).ToList(), options);
        foreach (var textGroup in textGroups)
            elements.AddRange(PxaElementBuilder.BuildTextElements(textGroup, placement, pixels, baselineHeightPx, splitRuns: true));

        return new DesignBuildResult(
            BuildDesignDto(name, pageWidth, pageHeight, elements),
            BuildLayoutDiagnostics(ruleSegments, enrichedTables));
    }

    private static DesignExportDto BuildDesignDto(
        string name,
        double pageWidth,
        double pageHeight,
        List<ElementDto> elements) =>
        new()
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
                    Subject = "Converted with PXA Image OCR Converter",
                },
            },
        };

    // Median word-box height across the document (falls back to median line height, then 1).
    // Used to clamp per-element font sizing so outlier OCR boxes don't explode.
    private static double EstimateBaselineTextHeight(IReadOnlyList<OcrLine> lines)
    {
        var wordHeights = lines
            .SelectMany(l => l.Words)
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .Select(w => (double)Math.Max(1, w.Bounds.Height))
            .ToList();
        if (wordHeights.Count > 0)
            return Median(wordHeights);

        var lineHeights = lines
            .Select(l => (double)Math.Max(1, l.Bounds.Height))
            .ToList();
        return lineHeights.Count > 0 ? Median(lineHeights) : 1.0;
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 1
            ? values[mid]
            : (values[mid - 1] + values[mid]) / 2.0;
    }

    // Snapshot pixels for visual detection, downscaling above the cap so flood-fill stays fast
    // on large scans. Returns the actual detection scale (detWidth/sourceWidth) and the resized
    // bitmap to dispose (null when the source is used directly).
    private static (OcrPixels Pixels, double Scale, SKBitmap? Owned) BuildDetectionPixels(SKBitmap source, long cap)
    {
        var pixelCount = (long)source.Width * source.Height;
        if (cap <= 0 || pixelCount <= cap)
            return (new OcrPixels(source), 1.0, null);

        var scale = Math.Sqrt(cap / (double)pixelCount);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var resized = source.Resize(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (resized is null)
            return (new OcrPixels(source), 1.0, null);

        return (new OcrPixels(resized), resized.Width / (double)source.Width, resized);
    }

    private static OcrBoundingBox ScaleBounds(OcrBoundingBox bounds, double factor) =>
        new(
            (int)Math.Round(bounds.X * factor),
            (int)Math.Round(bounds.Y * factor),
            Math.Max(1, (int)Math.Round(bounds.Width * factor)),
            Math.Max(1, (int)Math.Round(bounds.Height * factor)));

    private static OcrBoundingBox GetTableWordBounds(OcrTableCandidate table) =>
        OcrLayoutHelpers.UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds)));

    // Modes that reconstruct text positioned on the page itself (no original-image layer) and so
    // want the page sized to the scan's aspect rather than letterboxed.
    private static bool IsAspectFitLayout(string layoutMode) =>
        string.Equals(layoutMode, "text-only", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(layoutMode, "text-background", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldDetectShapes(ImageToPdfConversionOptions options) =>
        string.Equals(options.LayoutMode, "structured", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(options.LayoutMode, "shapes", StringComparison.OrdinalIgnoreCase);

    private static ImageToPdfLayoutDiagnostics BuildLayoutDiagnostics(
        IReadOnlyList<RuleSegment> ruleSegments,
        IReadOnlyList<OcrTableCandidate> tableCandidates)
    {
        var contrasts = ruleSegments
            .Select(s => s.Contrast)
            .Where(c => c > 0)
            .ToArray();

        return new ImageToPdfLayoutDiagnostics
        {
            Rules = new ImageToPdfRuleDiagnostics
            {
                SegmentCount = ruleSegments.Count,
                HorizontalSegmentCount = ruleSegments.Count(s => s.Orientation == RuleOrientation.Horizontal),
                VerticalSegmentCount = ruleSegments.Count(s => s.Orientation == RuleOrientation.Vertical),
                AverageContrast = contrasts.Length == 0 ? 0 : Math.Round(contrasts.Average(), 2),
                MaxContrast = contrasts.Length == 0 ? 0 : Math.Round(contrasts.Max(), 2),
                SampleSegments = ruleSegments
                    .OrderByDescending(s => s.Length)
                    .ThenBy(s => s.Y)
                    .ThenBy(s => s.X)
                    .Take(50)
                    .Select(s => new ImageToPdfRuleSegmentDiagnostics
                    {
                        Orientation = s.Orientation == RuleOrientation.Horizontal ? "horizontal" : "vertical",
                        X = s.X,
                        Y = s.Y,
                        Length = s.Length,
                        Contrast = Math.Round(s.Contrast, 2),
                    })
                    .ToArray(),
            },
            TableCandidates = tableCandidates
                .Select(BuildTableCandidateDiagnostics)
                .ToArray(),
        };
    }

    private static ImageToPdfTableCandidateDiagnostics BuildTableCandidateDiagnostics(OcrTableCandidate table)
    {
        var wordBounds = OcrLayoutHelpers.UnionBounds(table.Lines.SelectMany(l => l.Words.Select(w => w.Bounds)));
        var ruleBounds = table.RuleBounds;
        var backgroundBounds = table.BackgroundBounds;
        var rowAnchors = table.RowGroups
            .Select(row =>
            {
                var bounds = OcrLayoutHelpers.UnionBounds(row.Select(l => l.Bounds));
                return Math.Round(bounds.Y + bounds.Height / 2.0, 2);
            })
            .ToArray();

        var hasVisualEvidence = ruleBounds is not null || backgroundBounds is not null;
        return new ImageToPdfTableCandidateDiagnostics
        {
            Detector = ruleBounds is not null
                ? "rule-bounded-table"
                : backgroundBounds is not null ? "background-bounded-table" : table.Detector,
            Status = hasVisualEvidence ? "accepted" : "rejected-no-visible-table-lines",
            RejectionReason = hasVisualEvidence
                ? (ruleBounds is null ? table.RuleRejectionReason : null)
                : (table.RuleRejectionReason ?? "no-visible-table-lines"),
            SourceBoundsPx = $"{wordBounds.X},{wordBounds.Y},{wordBounds.Width},{wordBounds.Height}",
            RuleBoundsPx = ruleBounds is null ? null : $"{ruleBounds.X},{ruleBounds.Y},{ruleBounds.Width},{ruleBounds.Height}",
            BackgroundBoundsPx = backgroundBounds is null ? null : $"{backgroundBounds.X},{backgroundBounds.Y},{backgroundBounds.Width},{backgroundBounds.Height}",
            RowCount = table.RowGroups.Count,
            ColumnCount = table.ColumnAnchors.Count,
            Confidence = Math.Round(table.Lines.Count == 0 ? 0 : table.Lines.Average(l => l.Confidence), 4),
            ColumnAnchors = table.ColumnAnchors.Select(a => Math.Round(a, 2)).ToArray(),
            RowAnchors = rowAnchors,
        };
    }

    private sealed record DesignBuildResult(DesignExportDto Design, ImageToPdfLayoutDiagnostics Diagnostics);

    private static IReadOnlyList<string> BuildWarnings(
        double dpiX,
        double dpiY,
        long pixelCount,
        double ocrScaleFactor,
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
        if (ocrScaleFactor < 0.999)
            warnings.Add("Large image was downscaled for OCR to keep import responsive; coordinates were mapped back to the source image.");
        if (pixelCount > RuleShapeMaxPixels)
            warnings.Add("Very large image: shape detection was skipped to keep conversion responsive; OCR text and tables were still produced.");
        else if (pixelCount > FloodFillMaxPixels)
            warnings.Add("Large image: filled-area, circle and image-region detection were skipped to keep conversion responsive; text, tables and basic shapes were still produced.");
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

        // Pivot the contrast stretch around the image's mean luminance rather than a fixed 128.
        // A fixed midpoint pushes any text lighter than mid-grey (e.g. light-grey footer text on a
        // white page) further toward white, washing it out before OCR. Pivoting around the page's
        // actual mean keeps text that is darker than its background getting darker (more legible)
        // regardless of the text's absolute tone, so light footer text survives.
        var pivot = contrast ? Math.Clamp(ComputeMeanLuma(source), 80, 200) : 128.0;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                var luma = 0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue;
                var value = contrast
                    ? Math.Clamp((luma - pivot) * contrastFactor + pivot, 0, 255)
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

    /// <summary>Mean perceptual luminance of the image, sampled for speed on large bitmaps.</summary>
    private static double ComputeMeanLuma(SKBitmap source)
    {
        double sum = 0;
        long count = 0;
        var step = Math.Max(1, Math.Max(source.Width, source.Height) / 400);
        for (var y = 0; y < source.Height; y += step)
        {
            for (var x = 0; x < source.Width; x += step)
            {
                var c = source.GetPixel(x, y);
                sum += 0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue;
                count++;
            }
        }
        return count == 0 ? 128 : sum / count;
    }

    private static SKBitmap ResizeForOcr(
        SKBitmap source,
        IReadOnlyList<string> preprocessingSteps,
        out IReadOnlyList<string> effectiveSteps,
        out double scaleFactor)
    {
        var pixelCount = (long)source.Width * source.Height;
        if (pixelCount <= MaxOcrImagePixels)
        {
            effectiveSteps = preprocessingSteps;
            scaleFactor = 1;
            return source.Copy();
        }

        scaleFactor = Math.Sqrt(MaxOcrImagePixels / (double)pixelCount);
        var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scaleFactor));
        var targetHeight = Math.Max(1, (int)Math.Round(source.Height * scaleFactor));
        var imageInfo = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        var resized = source.Resize(imageInfo, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? source.Copy();
        effectiveSteps = [.. preprocessingSteps, $"ocr-scale:{Math.Round(scaleFactor, 4)}"];
        return resized;
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
