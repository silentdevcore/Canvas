using PXA.Core.Contracts;
using PXA.FileImporter.ImageAnalysis;
using PXA.FileImporter.ImageAnalysis.Analysis;
using SkiaSharp;
using System.Text.Json;

namespace PXA.FileImporter.ImageAnalysis.Tests;

public class RealSampleValidationTests
{
    private const string OverlayEnvVar = "IMAGE_ANALYSIS_WRITE_REAL_SAMPLE_OVERLAYS";
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public sealed class RealSampleExpectation
    {
        public List<string> ExpectedTextFragments { get; init; } = [];
        public int? MinTextLineCount { get; init; }
        public double? MinGlyphExactMatchRate { get; init; }
        public int? ExpectedElementCount { get; init; }
        public double? MaxElementCountNoise { get; init; }
        public int? MaxElementCount { get; init; }
        public double? MaxLowConfidenceGlyphRate { get; init; }
        public double? MaxRuntimeMs { get; init; }
    }

    [Fact]
    public void RealSamples_MeetExpectations()
    {
        var expectationFiles = FindExpectationFiles();
        if (expectationFiles.Count == 0)
            return;

        foreach (var expectationFile in expectationFiles)
        {
            var expectation = LoadExpectation(expectationFile);
            string sampleName = GetSampleName(expectationFile);
            string imagePath = FindMatchingImage(expectationFile, sampleName);

            using var bitmap = SKBitmap.Decode(imagePath);
            Assert.NotNull(bitmap);

            bool writeOverlay = ShouldWriteOverlays();
            var result = ImageAnalysisFileImporter.ImportWithAnalysis(
                bitmap,
                $"real-sample-{sampleName}",
                targetWidthPt: null,
                targetHeightPt: null,
                options: new ImageAnalysisOptions
                {
                    IncludeDebugOverlay = writeOverlay,
                });

            if (writeOverlay)
                WriteOverlay(sampleName, result);

            AssertExpectation(sampleName, expectation, result);
        }
    }

    private static IReadOnlyList<string> FindExpectationFiles()
    {
        string fixtureDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "RealSamples");

        if (!Directory.Exists(fixtureDirectory))
            return [];

        return Directory
            .EnumerateFiles(fixtureDirectory, "*.expected.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RealSampleExpectation LoadExpectation(string expectationFile)
    {
        using var stream = File.OpenRead(expectationFile);
        var expectation = JsonSerializer.Deserialize<RealSampleExpectation>(stream, JsonOptions);
        Assert.NotNull(expectation);

        if (expectation.MaxElementCountNoise.HasValue && !expectation.ExpectedElementCount.HasValue)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(expectationFile)} sets maxElementCountNoise but does not set expectedElementCount.");
        }

        return expectation;
    }

    private static string GetSampleName(string expectationFile)
    {
        string fileName = Path.GetFileName(expectationFile);
        return fileName[..^".expected.json".Length];
    }

    private static string FindMatchingImage(string expectationFile, string sampleName)
    {
        string directory = Path.GetDirectoryName(expectationFile)
            ?? throw new InvalidOperationException($"Unable to resolve directory for {expectationFile}.");

        foreach (string extension in ImageExtensions)
        {
            string candidate = Path.Combine(directory, sampleName + extension);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            $"No matching image found for {Path.GetFileName(expectationFile)}. Expected {sampleName}.png, .jpg, or .jpeg.");
    }

    private static void AssertExpectation(
        string sampleName,
        RealSampleExpectation expectation,
        ImageAnalysisImportResult result)
    {
        string actualText = CombinedText(result.Design);

        foreach (string fragment in expectation.ExpectedTextFragments)
        {
            Assert.True(
                actualText.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                $"{sampleName}: expected text fragment '{fragment}' in '{actualText}'.");
        }

        if (expectation.MinTextLineCount.HasValue)
        {
            Assert.True(
                result.Diagnostics.TextLineCount >= expectation.MinTextLineCount.Value,
                $"{sampleName}: text line count {result.Diagnostics.TextLineCount} below {expectation.MinTextLineCount.Value}.");
        }

        if (expectation.MinGlyphExactMatchRate.HasValue)
        {
            string expectedText = NormalizeText(string.Join(" ", expectation.ExpectedTextFragments));
            double glyphExactMatchRate = CalculateGlyphExactMatchRate(expectedText, actualText);
            Assert.True(
                glyphExactMatchRate >= expectation.MinGlyphExactMatchRate.Value,
                $"{sampleName}: glyph exact-match rate {glyphExactMatchRate:0.###} below {expectation.MinGlyphExactMatchRate.Value:0.###}. Actual text: '{actualText}'.");
        }

        if (expectation.MaxElementCountNoise.HasValue && expectation.ExpectedElementCount.HasValue)
        {
            double elementCountNoise = Math.Abs(result.Diagnostics.ElementCount - expectation.ExpectedElementCount.Value)
                / (double)expectation.ExpectedElementCount.Value;
            Assert.True(
                elementCountNoise <= expectation.MaxElementCountNoise.Value,
                $"{sampleName}: element count noise {elementCountNoise:0.###} above {expectation.MaxElementCountNoise.Value:0.###}. Element count: {result.Diagnostics.ElementCount}.");
        }

        if (expectation.MaxElementCount.HasValue)
        {
            Assert.True(
                result.Diagnostics.ElementCount <= expectation.MaxElementCount.Value,
                $"{sampleName}: element count {result.Diagnostics.ElementCount} above {expectation.MaxElementCount.Value}.");
        }

        if (expectation.MaxLowConfidenceGlyphRate.HasValue)
        {
            Assert.True(
                result.Diagnostics.LowConfidenceGlyphRate <= expectation.MaxLowConfidenceGlyphRate.Value,
                $"{sampleName}: low-confidence glyph rate {result.Diagnostics.LowConfidenceGlyphRate:0.###} above {expectation.MaxLowConfidenceGlyphRate.Value:0.###}.");
        }

        if (expectation.MaxRuntimeMs.HasValue)
        {
            Assert.True(
                result.Diagnostics.RuntimeMs <= expectation.MaxRuntimeMs.Value,
                $"{sampleName}: runtime {result.Diagnostics.RuntimeMs:0.###}ms above {expectation.MaxRuntimeMs.Value:0.###}ms.");
        }
    }

    private static string CombinedText(DesignExportDto design)
    {
        var textElements = design.Pages
            .SelectMany(page => page.Elements)
            .Where(element => element.Type == "text")
            .OrderBy(element => element.Y)
            .ThenBy(element => element.X)
            .Select(element => element.Content ?? "");

        return NormalizeText(string.Join(" ", textElements));
    }

    private static double CalculateGlyphExactMatchRate(string expectedText, string actualText)
    {
        int maxTextLength = Math.Max(expectedText.Length, actualText.Length);
        return maxTextLength == 0
            ? 1
            : Math.Round(Math.Max(0, 1 - EditDistance(expectedText, actualText) / (double)maxTextLength), 3);
    }

    private static string NormalizeText(string text) =>
        string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static int EditDistance(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
            dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++)
            dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }

    private static bool ShouldWriteOverlays()
    {
        string? value = Environment.GetEnvironmentVariable(OverlayEnvVar);
        return value is "1" or "true" or "TRUE" or "yes" or "YES";
    }

    private static void WriteOverlay(string sampleName, ImageAnalysisImportResult result)
    {
        if (result.DebugOverlayPng is null)
            return;

        string outputDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestResults",
            "ImageAnalysisOverlays");
        Directory.CreateDirectory(outputDirectory);

        string outputPath = Path.Combine(outputDirectory, sampleName + ".overlay.png");
        File.WriteAllBytes(outputPath, result.DebugOverlayPng);
    }
}
