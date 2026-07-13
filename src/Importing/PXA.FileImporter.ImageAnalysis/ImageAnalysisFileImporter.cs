using PXA.Core.Contracts;
using PXA.FileImporter.ImageAnalysis.Analysis;
using PXA.FileImporter.ImageAnalysis.Templates;
using SkiaSharp;
using System.Diagnostics;

namespace PXA.FileImporter.ImageAnalysis;

/// <summary>
/// Converts a raster image into a <see cref="DesignExportDto"/> by running the
/// custom 5-phase analysis engine:
///   1. Preprocessing (scale, grayscale, binarise)
///   2. Colour &amp; region analysis
///   3. Shape detection (edges, rectangles, lines, ellipses)
///   4. Text engine (connected components → char recognition)
///   5. Scene assembly → DTO mapping
/// </summary>
public sealed class ImageAnalysisFileImporter : PXA.FileImporter.IFileImporter
{
    /// <summary>
    /// Accepted file extensions. Not registered as <see cref="IFileImporter"/> to avoid
    /// extension collision with <c>ImageFileImporter</c>; the controller injects this
    /// class directly for the <c>import-image-analysis</c> endpoint.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedExtensions = ["png", "jpg", "jpeg"];

    IReadOnlyList<string> PXA.FileImporter.IFileImporter.SupportedExtensions => SupportedExtensions;

    Task<DesignExportDto> PXA.FileImporter.IFileImporter.ImportAsync(Stream stream, string? name) =>
        ImportAsync(stream, name);

    public async Task<DesignExportDto> ImportAsync(
        Stream stream,
        string? name          = null,
        double? targetWidthPt = null,
        double? targetHeightPt = null)
    {
        var result = await ImportWithAnalysisAsync(
            stream,
            name,
            targetWidthPt,
            targetHeightPt,
            ImageAnalysisOptions.Default);
        return result.Design;
    }

    public async Task<ImageAnalysisImportResult> ImportWithAnalysisAsync(
        Stream stream,
        string? name           = null,
        double? targetWidthPt  = null,
        double? targetHeightPt = null,
        bool includeDebugOverlay = false)
    {
        return await ImportWithAnalysisAsync(
            stream,
            name,
            targetWidthPt,
            targetHeightPt,
            new ImageAnalysisOptions { IncludeDebugOverlay = includeDebugOverlay });
    }

    public async Task<ImageAnalysisImportResult> ImportWithAnalysisAsync(
        Stream stream,
        string? name,
        double? targetWidthPt = null,
        double? targetHeightPt = null,
        ImageAnalysisOptions? options = null)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.Position = 0;

        using var bitmap = SKBitmap.Decode(ms)
            ?? throw new InvalidOperationException(
                "Unable to decode image — unsupported or corrupt file.");

        return ImportWithAnalysis(
            bitmap,
            name ?? "Analysed Image",
            targetWidthPt,
            targetHeightPt,
            options ?? ImageAnalysisOptions.Default);
    }

    public static DesignExportDto Import(
        SKBitmap source,
        string   name,
        double?  targetWidthPt  = null,
        double?  targetHeightPt = null)
    {
        return ImportWithAnalysis(
            source,
            name,
            targetWidthPt,
            targetHeightPt,
            ImageAnalysisOptions.Default).Design;
    }

    public static ImageAnalysisImportResult ImportWithAnalysis(
        SKBitmap source,
        string   name,
        double?  targetWidthPt = null,
        double?  targetHeightPt = null,
        ImageAnalysisOptions? options = null)
    {
        options ??= ImageAnalysisOptions.Default;

        long memoryBefore = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();

        using var prepared = Preprocessor.Prepare(source);
        var colors     = ColorAnalyzer.Analyze(prepared);
        var shapes     = ShapeDetector.Detect(prepared, colors);
        var texts      = TextEngine.Analyze(prepared);
        var primitives = SceneAssembler.Assemble(colors, shapes, texts);

        var design = SceneAssembler.ToDesign(
            primitives,
            background:     colors.Background,
            imageWidthPx:   prepared.Width,
            imageHeightPx:  prepared.Height,
            scaleFactor:    prepared.ScaleFactor,
            name:           name,
            targetWidthPt:  targetWidthPt,
            targetHeightPt: targetHeightPt,
            options:        options,
            fallbackImageDataUri: options.IncludeFallbackImageLayer
                ? EncodeSourceImageDataUri(prepared.Original)
                : null);

        stopwatch.Stop();
        long memoryAfter = GC.GetTotalMemory(false);

        return new ImageAnalysisImportResult
        {
            Design = design,
            Diagnostics = BuildDiagnostics(
                source,
                prepared,
                colors,
                shapes,
                texts,
                design,
                options,
                stopwatch.Elapsed.TotalMilliseconds,
                memoryAfter - memoryBefore),
            DebugOverlayPng = options.IncludeDebugOverlay
                ? DebugOverlayRenderer.RenderPng(prepared, colors, shapes, texts)
                : null,
        };
    }

    private static ImageAnalysisDiagnostics BuildDiagnostics(
        SKBitmap source,
        PreparedImage prepared,
        ColorAnalysisResult colors,
        ShapeDetectionResult shapes,
        TextAnalysisResult texts,
        DesignExportDto design,
        ImageAnalysisOptions options,
        double runtimeMs,
        long memoryDeltaBytes)
    {
        int wordCount = texts.Lines.Sum(l => l.Words.Count);
        int glyphCount = texts.Lines.Sum(l => l.Words.Sum(w => w.Chars.Count));
        int lowConfidenceGlyphCount = texts.Lines.Sum(l =>
            l.Words.Sum(w => w.Chars.Count(c => c.Value == '?' || c.Confidence < 0.5)));
        int elementCount = design.Pages.Sum(p => p.Elements.Count);

        var warnings = new List<string>();
        if (prepared.ScaleFactor < 1.0)
            warnings.Add("Input image was downscaled for analysis.");
        if (texts.Lines.Count == 0)
            warnings.Add("No text lines detected.");
        if (glyphCount > 0 && lowConfidenceGlyphCount > 0)
            warnings.Add("Some glyphs were low-confidence or unresolved.");
        if (HasLowConfidenceElements(design, options.LowConfidenceThreshold))
            warnings.Add("Some elements are below the configured confidence threshold.");
        if (options.IncludeFallbackImageLayer)
            warnings.Add("Fallback image layer included.");
        if (colors.Regions.Count == 0 && shapes.Shapes.Count == 0 && texts.Lines.Count == 0)
            warnings.Add("No editable foreground elements detected.");

        return new ImageAnalysisDiagnostics
        {
            SourceWidthPx = source.Width,
            SourceHeightPx = source.Height,
            WorkingWidthPx = prepared.Width,
            WorkingHeightPx = prepared.Height,
            ScaleFactor = prepared.ScaleFactor,
            ColorRegionCount = colors.Regions.Count,
            ShapeCount = shapes.Shapes.Count,
            TextLineCount = texts.Lines.Count,
            WordCount = wordCount,
            GlyphCount = glyphCount,
            LowConfidenceGlyphCount = lowConfidenceGlyphCount,
            LowConfidenceGlyphRate = glyphCount == 0
                ? 0
                : Math.Round(lowConfidenceGlyphCount / (double)glyphCount, 4),
            ElementCount = elementCount,
            RuntimeMs = Math.Round(runtimeMs, 3),
            MemoryDeltaBytes = memoryDeltaBytes,
            GlyphTemplateProfile = CharacterTemplates.ProfileId,
            RecognitionReadiness = "benchmark-gated",
            RecognitionFidelityScope = "synthetic-business-documents-v1",
            Warnings = warnings,
        };
    }

    private static bool HasLowConfidenceElements(DesignExportDto design, double threshold)
    {
        return design.Pages
            .SelectMany(p => p.Elements)
            .Any(e =>
                e.Style is not null &&
                e.Style.TryGetValue("imageAnalysisConfidence", out var value) &&
                Convert.ToDouble(value) < threshold);
    }

    private static string EncodeSourceImageDataUri(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return $"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
    }
}
