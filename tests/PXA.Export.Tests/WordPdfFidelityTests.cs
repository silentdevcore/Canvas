using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PXA.Infrastructure.Word;
using PXA.WebApi.Infrastructure;

namespace PXA.Export.Tests;

public sealed class WordPdfFidelityTests
{
    private sealed record SampleReport(
        string Sample,
        bool BaselineGenerated,
        bool WordGenerated,
        bool ConvertedPdfGenerated,
        double? WidthDriftPt,
        double? HeightDriftPt,
        double? PixelDiffRatio,
        double? FidelityScore,
        string? Notes);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void SamplePack_ProducesPdfAndDocxArtifacts_AndComparesGeometry_WhenLibreOfficeAvailable()
    {
        var repoRoot = FindRepoRoot();
        var samplesDir = Path.Combine(repoRoot, "checklists", "word-fidelity-samples");
        Assert.True(Directory.Exists(samplesDir), $"Sample pack directory not found: {samplesDir}");

        var sampleFiles = Directory.GetFiles(samplesDir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(sampleFiles);

        var runDir = Path.Combine(repoRoot, "tests", "Canvas.Export.Tests", "Fidelity", "artifacts", "latest");
        Directory.CreateDirectory(runDir);

        var processedCount = 0;
        var comparedCount = 0;
        var hasLibreOffice = ResolveSofficeBinary() is not null;
        var canRenderPng = ResolvePngRendererBinary() is not null;
        var canImageDiff = ResolveImageDiffBinary() is not null;
        var visualDiffThreshold = ResolveVisualDiffThreshold();
        var report = new List<SampleReport>();

        foreach (var sampleFile in sampleFiles)
        {
            var design = LoadDesign(sampleFile);
            var sampleName = Path.GetFileNameWithoutExtension(sampleFile);

            var sampleDir = Path.Combine(runDir, sampleName);
            Directory.CreateDirectory(sampleDir);

            var baselinePdfPath = Path.Combine(sampleDir, "baseline.pdf");
            var wordDocxPath = Path.Combine(sampleDir, "word.docx");
            var convertedPdfPath = Path.Combine(sampleDir, "word-converted.pdf");
            var baselinePngPath = Path.Combine(sampleDir, "baseline-page1.png");
            var convertedPngPath = Path.Combine(sampleDir, "word-page1.png");
            var diffPngPath = Path.Combine(sampleDir, "diff-page1.png");

            var wordDocx = new WordDocumentExporter().Export(design);
            File.WriteAllBytes(wordDocxPath, wordDocx);
            Assert.True(wordDocx.Length > 0, $"Word DOCX empty for sample {sampleName}");

            var baselinePdf = DesignJsonMapper.MapToPdfDocument(design).ToBytes();

            File.WriteAllBytes(baselinePdfPath, baselinePdf);
            Assert.True(baselinePdf.Length > 0, $"Baseline PDF empty for sample {sampleName}");
            processedCount++;

            var convertedGenerated = false;
            double? widthDrift = null;
            double? heightDrift = null;
            double? pixelDiffRatio = null;
            double? fidelityScore = null;
            string? notes = null;

            if (!TryConvertDocxToPdf(wordDocxPath, convertedPdfPath))
            {
                // Fidelity comparison requires LibreOffice in CI/dev machine.
                report.Add(new SampleReport(sampleName, true, true, false, null, null, null,
                    null,
                    "LibreOffice unavailable or conversion failed"));
                continue;
            }
            convertedGenerated = true;

            Assert.True(File.Exists(convertedPdfPath), $"Converted PDF missing for sample {sampleName}");
            var convertedPdf = File.ReadAllBytes(convertedPdfPath);

            var baselineSize = ParseFirstPageMediaBox(baselinePdf);
            var convertedSize = ParseFirstPageMediaBox(convertedPdf);

            Assert.True(baselineSize is not null, $"Could not parse baseline MediaBox for sample {sampleName}");
            Assert.True(convertedSize is not null, $"Could not parse converted MediaBox for sample {sampleName}");

            widthDrift = Math.Abs(baselineSize!.Value.width - convertedSize!.Value.width);
            heightDrift = Math.Abs(baselineSize.Value.height - convertedSize.Value.height);

            // 6pt threshold keeps page geometry close while tolerating engine differences.
            Assert.True(widthDrift <= 6, $"Width drift too high for {sampleName}: {widthDrift:0.##}pt");
            Assert.True(heightDrift <= 6, $"Height drift too high for {sampleName}: {heightDrift:0.##}pt");

            if (canRenderPng && TryRenderFirstPagePng(baselinePdfPath, baselinePngPath) &&
                TryRenderFirstPagePng(convertedPdfPath, convertedPngPath))
            {
                if (canImageDiff && TryComputeImageDiffRatio(baselinePngPath, convertedPngPath, diffPngPath, out var ratio))
                {
                    pixelDiffRatio = ratio;
                    fidelityScore = Math.Clamp((1.0 - ratio) * 100.0, 0.0, 100.0);
                    notes = ratio > 0.08
                        ? $"High visual diff ratio: {ratio:0.####}"
                        : $"Visual diff ratio: {ratio:0.####}";
                }
                else
                {
                    notes = "PNG snapshots generated; image diff tool unavailable";
                }
            }

            report.Add(new SampleReport(sampleName, true, true, convertedGenerated, widthDrift, heightDrift, pixelDiffRatio, fidelityScore, notes));
            comparedCount++;
        }

        var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(runDir, "fidelity-report.json"), reportJson);

        var scoreLines = report
            .Where(r => r.FidelityScore.HasValue)
            .OrderByDescending(r => r.FidelityScore)
            .Select(r => $"{r.Sample}: score={r.FidelityScore:0.##}, diff={(r.PixelDiffRatio ?? 0):0.####}")
            .ToArray();
        File.WriteAllLines(Path.Combine(runDir, "fidelity-scores.txt"),
            scoreLines.Length > 0 ? scoreLines : ["No visual scores generated in this run."]);

        Assert.True(processedCount > 0, "No sample produced both baseline PDF and DOCX artifacts.");

        // If LibreOffice is available, compare every processed sample.
        if (hasLibreOffice)
        {
            var conversionFailures = report
                .Where(r => !r.ConvertedPdfGenerated)
                .Select(r => r.Sample)
                .OrderBy(static s => s, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.True(
                conversionFailures.Length == 0,
                $"Some samples failed DOCX->PDF conversion: {string.Join(", ", conversionFailures)}");
        }

        if (hasLibreOffice && canRenderPng && canImageDiff)
        {
            var scored = report.Where(r => r.PixelDiffRatio.HasValue).ToList();
            Assert.True(scored.Count > 0, "Visual diff tools are available but no pixel diff ratios were produced.");

            var failures = scored
                .Where(r => (r.PixelDiffRatio ?? 0) > visualDiffThreshold)
                .OrderByDescending(r => r.PixelDiffRatio)
                .Take(5)
                .ToList();

            if (failures.Count > 0)
            {
                var topMismatches = string.Join(", ",
                    failures.Select(f => $"{f.Sample}={(f.PixelDiffRatio ?? 0):0.####}"));

                Assert.Fail($"Visual diff threshold exceeded (>{visualDiffThreshold:0.####}). Top mismatches: {topMismatches}");
            }
        }
    }

    private static double ResolveVisualDiffThreshold()
    {
        var raw = Environment.GetEnvironmentVariable("CANVAS_WORD_VISUAL_DIFF_THRESHOLD");
        if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return Math.Clamp(parsed, 0.0, 1.0);

        return 0.15;
    }

    private static DesignExportDto LoadDesign(string sampleFile)
    {
        var json = File.ReadAllText(sampleFile);
        var design = JsonSerializer.Deserialize<DesignExportDto>(json, JsonOptions);
        Assert.NotNull(design);
        return design!;
    }

    private static (double width, double height)? ParseFirstPageMediaBox(byte[] pdfBytes)
    {
        var text = Encoding.ASCII.GetString(pdfBytes);
        var match = Regex.Match(text, @"/MediaBox\s*\[\s*0\s+0\s+([0-9]+(?:\.[0-9]+)?)\s+([0-9]+(?:\.[0-9]+)?)\s*\]");
        if (!match.Success) return null;

        if (!double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var width))
            return null;
        if (!double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var height))
            return null;

        return (width, height);
    }

    private static bool TryConvertDocxToPdf(string docxPath, string outputPdfPath)
    {
        var soffice = ResolveSofficeBinary();
        if (soffice is null)
            return false;

        var outDir = Path.GetDirectoryName(outputPdfPath)!;
        var psi = new ProcessStartInfo
        {
            FileName = soffice,
            ArgumentList =
            {
                "--headless",
                "--convert-to", "pdf",
                "--outdir", outDir,
                docxPath
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null)
            return false;

        if (!proc.WaitForExit(120_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return false;
        }

        var generated = Path.Combine(outDir, Path.GetFileNameWithoutExtension(docxPath) + ".pdf");
        if (!File.Exists(generated))
            return false;

        if (!string.Equals(generated, outputPdfPath, StringComparison.Ordinal))
            File.Move(generated, outputPdfPath, overwrite: true);

        return proc.ExitCode == 0 && File.Exists(outputPdfPath);
    }

    private static string? ResolveSofficeBinary()
    {
        var macAppBinary = "/Applications/LibreOffice.app/Contents/MacOS/soffice";
        if (File.Exists(macAppBinary))
            return macAppBinary;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "which",
                ArgumentList = { "soffice" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5_000);

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && File.Exists(output))
                return output;
        }
        catch
        {
            // Best-effort lookup only.
        }

        return null;
    }

    private static string? ResolvePngRendererBinary()
    {
        return ResolveBinary("pdftoppm");
    }

    private static string? ResolveImageDiffBinary()
    {
        return ResolveBinary("compare") ?? ResolveBinary("magick");
    }

    private static string? ResolveBinary(string name)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "which",
                ArgumentList = { name },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5_000);

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && File.Exists(output))
                return output;
        }
        catch
        {
            // Best-effort lookup.
        }

        return null;
    }

    private static bool TryRenderFirstPagePng(string pdfPath, string pngPath)
    {
        var pdftoppm = ResolvePngRendererBinary();
        if (pdftoppm is null)
            return false;

        var prefix = Path.Combine(Path.GetDirectoryName(pngPath)!, Path.GetFileNameWithoutExtension(pngPath));
        var psi = new ProcessStartInfo
        {
            FileName = pdftoppm,
            ArgumentList =
            {
                "-f", "1",
                "-singlefile",
                "-png",
                pdfPath,
                prefix
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null)
            return false;

        proc.WaitForExit(30_000);
        var generated = prefix + ".png";
        if (!File.Exists(generated))
            return false;

        if (!string.Equals(generated, pngPath, StringComparison.Ordinal))
            File.Move(generated, pngPath, overwrite: true);

        return proc.ExitCode == 0 && File.Exists(pngPath);
    }

    private static bool TryComputeImageDiffRatio(string baselinePngPath, string convertedPngPath, string diffPngPath, out double ratio)
    {
        ratio = 0;

        var compare = ResolveBinary("compare");
        if (compare is not null)
        {
            return TryComputeImageDiffRatioWithCompare(compare, baselinePngPath, convertedPngPath, diffPngPath, out ratio);
        }

        var magick = ResolveBinary("magick");
        if (magick is not null)
        {
            return TryComputeImageDiffRatioWithMagick(magick, baselinePngPath, convertedPngPath, diffPngPath, out ratio);
        }

        return false;
    }

    private static bool TryComputeImageDiffRatioWithCompare(string compareBinary, string baselinePngPath, string convertedPngPath, string diffPngPath, out double ratio)
    {
        ratio = 0;
        var psi = new ProcessStartInfo
        {
            FileName = compareBinary,
            ArgumentList =
            {
                "-metric", "RMSE",
                baselinePngPath,
                convertedPngPath,
                diffPngPath
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null)
            return false;

        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30_000);

        return TryParseImageMagickRmse(stderr, out ratio);
    }

    private static bool TryComputeImageDiffRatioWithMagick(string magickBinary, string baselinePngPath, string convertedPngPath, string diffPngPath, out double ratio)
    {
        ratio = 0;
        var psi = new ProcessStartInfo
        {
            FileName = magickBinary,
            ArgumentList =
            {
                "compare",
                "-metric", "RMSE",
                baselinePngPath,
                convertedPngPath,
                diffPngPath
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null)
            return false;

        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30_000);

        return TryParseImageMagickRmse(stderr, out ratio);
    }

    private static bool TryParseImageMagickRmse(string stderr, out double ratio)
    {
        ratio = 0;
        if (string.IsNullOrWhiteSpace(stderr))
            return false;

        // Typical output: "1234.56 (0.0188394)"
        var match = Regex.Match(stderr, @"\(([0-9]+(?:\.[0-9]+)?)\)");
        if (!match.Success)
            return false;

        return double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out ratio);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PXA.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing PXA.sln.");
    }
}
