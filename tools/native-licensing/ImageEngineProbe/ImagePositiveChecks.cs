using System.Reflection;
using PVAD.FileImporter.ImageAnalysis;
using PVAD.FileImporter.ImageAnalysis.Analysis;
using PVAD.FileImporter.ImageAnalysis.Templates;
using SkiaSharp;

internal static class ImagePositiveChecks
{
    internal static async Task<Dictionary<MethodInfo, string>> Run(SKBitmap bitmap, byte[] png, List<string> checks)
    {
        using var prepared = Preprocessor.Prepare(bitmap);
        var colors = ColorAnalyzer.Analyze(prepared);
        var shapes = ShapeDetector.Detect(prepared, colors);
        var texts = TextEngine.Analyze(prepared);
        var primitives = SceneAssembler.Assemble(colors, shapes, texts);
        var edges = ShapeDetector.ComputeSobelEdgeMap(prepared.Grayscale);
        var horizontal = ShapeDetector.FindHorizontalSegments(edges, prepared.Width, prepared.Height);
        var vertical = ShapeDetector.FindVerticalSegments(edges, prepared.Width, prepared.Height);
        var blob = new BlobInfo { Bounds = new(2, 2, 15, 25), PixelCount = 20 };
        var patch = new float[1024];
        var calls = new Dictionary<MethodInfo, Func<Task>>();
        var analysis = new ImageAnalysisFileImporter();

        Add(typeof(Preprocessor), "Prepare", () => { using var value = Preprocessor.Prepare(bitmap); Equal(value.Width == bitmap.Width); });
        Add(typeof(Preprocessor), "ComputeOtsuThreshold", () => Equal(Preprocessor.ComputeOtsuThreshold(prepared.Grayscale) is >= 0 and <= 255));
        var quantizedWhite = new SKColor(248, 248, 248);
        Add(typeof(ColorAnalyzer), "Analyze", () => Equal(ColorAnalyzer.Analyze(prepared).Background == quantizedWhite));
        Add(typeof(ColorAnalyzer), "DetectBackground", () => Equal(ColorAnalyzer.DetectBackground(bitmap) == quantizedWhite));
        Add(typeof(ColorAnalyzer), "BuildPalette", () => Equal(ColorAnalyzer.BuildPalette(bitmap).Count > 0));
        Add(typeof(ColorAnalyzer), "SegmentRegions", () => _ = ColorAnalyzer.SegmentRegions(bitmap, SKColors.White));
        Add(typeof(ColorAnalyzer), "MergeAdjacentRegions", () => _ = ColorAnalyzer.MergeAdjacentRegions(colors.Regions, 2));
        Add(typeof(ColorAnalyzer), "DetectImageLikeRegions", () => _ = ColorAnalyzer.DetectImageLikeRegions(bitmap, SKColors.White, colors.Regions));
        Add(typeof(ShapeDetector), "Detect", () => _ = ShapeDetector.Detect(prepared, colors));
        Add(typeof(ShapeDetector), "ComputeSobelEdgeMap", () => Equal(ShapeDetector.ComputeSobelEdgeMap(prepared.Grayscale).Length == prepared.Width * prepared.Height));
        Add(typeof(ShapeDetector), "FindHorizontalSegments", () => _ = ShapeDetector.FindHorizontalSegments(edges, prepared.Width, prepared.Height));
        Add(typeof(ShapeDetector), "FindVerticalSegments", () => _ = ShapeDetector.FindVerticalSegments(edges, prepared.Width, prepared.Height));
        Add(typeof(ShapeDetector), "AssembleRectangles", () => _ = ShapeDetector.AssembleRectangles(horizontal, vertical));
        Add(typeof(ShapeDetector), "FindRoundedRectangles", () => _ = ShapeDetector.FindRoundedRectangles(prepared.Binary));
        Add(typeof(ShapeDetector), "FindFilledRectangles", () => _ = ShapeDetector.FindFilledRectangles(prepared.Binary));
        Add(typeof(ShapeDetector), "FindEllipses", () => _ = ShapeDetector.FindEllipses(prepared.Binary));
        Add(typeof(ShapeDetector), "FindIconClusters", () => _ = ShapeDetector.FindIconClusters(prepared.Binary));
        Add(typeof(ShapeDetector), "FindImageClusters", () => _ = ShapeDetector.FindImageClusters(prepared.Binary));
        Add(typeof(TextEngine), "Analyze", () => _ = TextEngine.Analyze(prepared));
        Add(typeof(TextEngine), "LabelConnectedComponents", () => _ = TextEngine.LabelConnectedComponents(prepared.Binary));
        Add(typeof(TextEngine), "FilterCharCandidates", () => _ = TextEngine.FilterCharCandidates([], prepared.Binary));
        Add(typeof(TextEngine), "AssembleLines", () => Equal(TextEngine.AssembleLines([]).Count == 0));
        Add(typeof(GlyphRecognizer), "Recognize", () => _ = GlyphRecognizer.Recognize(prepared.Binary, blob));
        Add(typeof(GlyphRecognizer), "CountEnclosedWhiteRegionsForTest", () => _ = GlyphRecognizer.CountEnclosedWhiteRegionsForTest(prepared.Binary, blob));
        Add(typeof(GlyphRecognizer), "ExtractPatchForTest", () => Equal(GlyphRecognizer.ExtractPatchForTest(prepared.Binary, blob.Bounds).Length == 1024));
        Add(typeof(GlyphRecognizer), "ProjectionProfileDistanceForTest", () => _ = GlyphRecognizer.ProjectionProfileDistanceForTest(patch, 'A'));
        Add(typeof(GlyphRecognizer), "ZoningDistanceForTest", () => _ = GlyphRecognizer.ZoningDistanceForTest(patch, 'A'));
        Add(typeof(CharacterTemplates), "TryGetTemplate", () => Equal(CharacterTemplates.TryGetTemplate('A', out var template) && template.Length == 1024));
        Add(typeof(CharacterTemplates), "TryGetBestTemplate", () => Equal(
            CharacterTemplates.TryGetTemplate('A', out var sample) &&
            CharacterTemplates.TryGetBestTemplate('A', sample, out var template, out var score) && template.Length == 1024 && score > 0));
        Add(typeof(CharacterTemplates), "Match", () => _ = CharacterTemplates.Match(patch));
        Add(typeof(CharacterTemplates), "MatchTop", () => Equal(CharacterTemplates.MatchTop(patch, 1).Count == 1));
        Add(typeof(SceneAssembler), "Assemble", () => _ = SceneAssembler.Assemble(colors, shapes, texts));
        Add(typeof(SceneAssembler), "ToDesign", () => Equal(SceneAssembler.ToDesign(primitives, SKColors.White, bitmap.Width, bitmap.Height, 1, "Synthetic scene").Pages.Count == 1));
        Add(typeof(DebugOverlayRenderer), "RenderPng", () => Equal(DebugOverlayRenderer.RenderPng(prepared, colors, shapes, texts).AsSpan().StartsWith(new byte[] { 137, 80, 78, 71 })));
        Add(typeof(ImageAnalysisFileImporter), "Import", () => Equal(ImageAnalysisFileImporter.Import(bitmap, "Synthetic image").Pages.Count == 1));
        Add(typeof(ImageAnalysisFileImporter), "ImportWithAnalysis", () => Equal(ImageAnalysisFileImporter.ImportWithAnalysis(bitmap, "Synthetic image").Design.Pages.Count == 1));
        var methods = typeof(ImageAnalysisFileImporter).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(method => !method.IsSpecialName);
        foreach (var method in methods)
            calls.Add(method, async () =>
            {
                using var input = new MemoryStream(png);
                var parameters = method.GetParameters();
                if (method.Name == "ImportAsync") Equal((await analysis.ImportAsync(input)).Pages.Count == 1);
                else if (parameters[^1].ParameterType == typeof(bool)) Equal((await analysis.ImportWithAnalysisAsync(input)).Design.Pages.Count == 1);
                else Equal((await analysis.ImportWithAnalysisAsync(input, "Synthetic image", null, null, new ImageAnalysisOptions())).Design.Pages.Count == 1);
                Equal(input.CanRead);
            });
        Equal(calls.Count == 39);
        var result = new Dictionary<MethodInfo, string>();
        foreach (var (method, call) in calls)
        {
            try { await call(); }
            catch (Exception error) { throw new InvalidOperationException("Positive image admission failed for " + method.DeclaringType!.Name + "." + method.Name, error); }
            var check = "positive:" + method.DeclaringType!.Name + ":" + method;
            checks.Add(check); result.Add(method, check);
        }
        return result;

        void Add(Type type, string name, Action action)
        {
            var method = type.GetMethod(name) ?? throw new MissingMethodException(type.FullName, name);
            calls.Add(method, () => { action(); return Task.CompletedTask; });
        }
    }

    private static void Equal(bool condition)
    { if (!condition) throw new InvalidOperationException("Image public-entry positive fixture failed."); }
}
