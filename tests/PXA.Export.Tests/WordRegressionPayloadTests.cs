using System.Text.Json;
using PXA.Infrastructure.Word;
using System.Diagnostics;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PXA.Export.Tests;

public sealed class WordRegressionPayloadTests
{
    [Fact]
    public void Word_Regression_SamplePayloads_ExportWithoutBlockingExceptions()
    {
        var repoRoot = FindRepoRoot();
        var samplesDir = Path.Combine(repoRoot, "checklists", "word-fidelity-samples");
        Assert.True(Directory.Exists(samplesDir), $"Sample pack directory not found: {samplesDir}");

        var sampleFiles = Directory.GetFiles(samplesDir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(sampleFiles);

        var exporter = new WordDocumentExporter();
        foreach (var sampleFile in sampleFiles)
        {
            var json = File.ReadAllText(sampleFile);
            var design = JsonSerializer.Deserialize<DesignExportDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(design);

            var ex = Record.Exception(() => exporter.Export(design!));
            Assert.Null(ex);

            var bytes = exporter.Export(design!);
            Assert.NotEmpty(bytes);
            Assert.True(bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B,
                $"Sample '{Path.GetFileName(sampleFile)}' did not produce a valid DOCX signature.");
        }
    }

    [Fact]
    public void Word_Regression_LargeDocument_50Pages_ExportsWithoutCrash()
    {
        var pages = new List<PageDto>();
        for (var i = 1; i <= 50; i++)
        {
            pages.Add(new PageDto
            {
                Id = $"p{i}",
                Elements =
                [
                    new ElementDto
                    {
                        Id = $"t{i}",
                        Type = "text",
                        X = 20,
                        Y = 30,
                        Width = 300,
                        Height = 40,
                        Content = $"Page {i}"
                    },
                    new ElementDto
                    {
                        Id = $"tbl{i}",
                        Type = "table",
                        X = 20,
                        Y = 90,
                        Width = 400,
                        Height = 180,
                        HeaderRow = true,
                        CellData =
                        [
                            ["Col1", "Col2"],
                            [$"R{i}-A", $"R{i}-B"]
                        ]
                    }
                ]
            });
        }

        var design = new DesignExportDto
        {
            Id = "large-50-pages",
            Name = "Large regression",
            Pages = pages
        };

        var bytes = new WordDocumentExporter().Export(design);
        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B);
    }

    [Fact]
    public void Word_Profile_LargeDocument_50Pages_WritesPerfArtifact()
    {
        var pages = new List<PageDto>();
        for (var i = 1; i <= 50; i++)
        {
            pages.Add(new PageDto
            {
                Id = $"pp{i}",
                Elements =
                [
                    new ElementDto
                    {
                        Id = $"txt{i}",
                        Type = "text",
                        X = 20,
                        Y = 20,
                        Width = 420,
                        Height = 24,
                        Content = $"Profiling page {i}"
                    },
                    new ElementDto
                    {
                        Id = $"tblp{i}",
                        Type = "table",
                        X = 20,
                        Y = 60,
                        Width = 450,
                        Height = 220,
                        HeaderRow = true,
                        CellData =
                        [
                            ["Metric", "Value", "Notes"],
                            ["Page", i.ToString(), "Perf test"],
                            ["Status", "OK", "Baseline"]
                        ]
                    }
                ]
            });
        }

        var design = new DesignExportDto
        {
            Id = "perf-50-pages",
            Name = "Perf profile",
            Pages = pages
        };

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        var processBefore = Process.GetCurrentProcess().WorkingSet64;
        var sw = Stopwatch.StartNew();

        var bytes = new WordDocumentExporter().Export(design);

        sw.Stop();
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
        var processAfter = Process.GetCurrentProcess().WorkingSet64;

        Assert.NotEmpty(bytes);
        Assert.True(bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B);

        var artifact = new
        {
            test = nameof(Word_Profile_LargeDocument_50Pages_WritesPerfArtifact),
            timestampUtc = DateTime.UtcNow,
            pages = 50,
            elapsedMs = sw.ElapsedMilliseconds,
            docxBytes = bytes.Length,
            managedMemoryBeforeBytes = memoryBefore,
            managedMemoryAfterBytes = memoryAfter,
            managedMemoryDeltaBytes = memoryAfter - memoryBefore,
            processWorkingSetBeforeBytes = processBefore,
            processWorkingSetAfterBytes = processAfter,
            processWorkingSetDeltaBytes = processAfter - processBefore
        };

        var repoRoot = FindRepoRoot();
        var perfDir = Path.Combine(repoRoot, "tests", "PXA.Export.Tests", "Fidelity", "artifacts", "latest", "perf");
        Directory.CreateDirectory(perfDir);
        var perfPath = Path.Combine(perfDir, "word-export-perf-50-pages.json");
        File.WriteAllText(perfPath, JsonSerializer.Serialize(artifact, new JsonSerializerOptions { WriteIndented = true }));
        Assert.True(File.Exists(perfPath));
    }

    [Fact]
    public void Word_Regression_TableStressSamples_RenderAsFixedLayoutTables()
    {
        var dense = LoadSample("dense-table-ledger.json");
        var wide = LoadSample("wide-table-report.json");

        foreach (var design in new[] { dense, wide })
        {
            var bytes = new WordDocumentExporter().Export(design);
            using var ms = new MemoryStream(bytes);
            using var doc = WordprocessingDocument.Open(ms, false);

            var table = doc.MainDocumentPart!.Document!.Body!.Descendants<Table>().FirstOrDefault();
            Assert.NotNull(table);

            var layout = table!.TableProperties?.GetFirstChild<TableLayout>();
            Assert.NotNull(layout);
            Assert.Equal(TableLayoutValues.Fixed, layout!.Type!.Value);
        }
    }

    [Fact]
    public void Word_Regression_ImageHeavySample_EmbedsImagesWithoutFallbackPlaceholder()
    {
        var design = LoadSample("image-and-caption.json");

        var bytes = new WordDocumentExporter().Export(design);
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);

        Assert.NotEmpty(doc.MainDocumentPart!.ImageParts);
        var bodyText = doc.MainDocumentPart.Document!.Body!.InnerText;
        Assert.DoesNotContain("[image unavailable]", bodyText, StringComparison.Ordinal);
    }

    [Fact]
    public void Word_Regression_UtilityElementSamples_ExportWithExpectedArtifacts()
    {
        var links = LoadSample("links-and-note.json");
        var form = LoadSample("form-elements.json");

        var linkBytes = new WordDocumentExporter().Export(links);
        using (var lms = new MemoryStream(linkBytes))
        using (var ldoc = WordprocessingDocument.Open(lms, false))
        {
            Assert.NotEmpty(ldoc.MainDocumentPart!.HyperlinkRelationships);
            Assert.Contains("Release Checklist", ldoc.MainDocumentPart.Document!.Body!.InnerText, StringComparison.OrdinalIgnoreCase);
        }

        var formBytes = new WordDocumentExporter().Export(form);
        using (var fms = new MemoryStream(formBytes))
        using (var fdoc = WordprocessingDocument.Open(fms, false))
        {
            var text = fdoc.MainDocumentPart!.Document!.Body!.InnerText;
            Assert.Contains("Signature", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("☐", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Word_Regression_TypographySample_PreservesHeadingAndInlineStyles()
    {
        var design = LoadSample("richtext-link-list.json");

        var bytes = new WordDocumentExporter().Export(design);
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);

        // Runs with RunProperties are the content runs inside WPS text boxes.
        // The outer runs wrapping Drawing elements have no RunProperties and are excluded.
        var runs = doc.MainDocumentPart!.Document!.Body!
            .Descendants<Run>()
            .Where(r => r.RunProperties != null)
            .ToList();
        Assert.NotEmpty(runs);

        var headingRun = runs.FirstOrDefault(r => r.InnerText.Contains("Quarterly Notes", StringComparison.Ordinal));
        Assert.NotNull(headingRun);

        var boldRun = runs.FirstOrDefault(r => r.InnerText.Contains("Status:", StringComparison.Ordinal));
        Assert.NotNull(boldRun);
        Assert.NotNull(boldRun!.RunProperties?.Bold);

        var italicRun = runs.FirstOrDefault(r => r.InnerText.Contains("Owner:", StringComparison.Ordinal));
        Assert.NotNull(italicRun);
        Assert.NotNull(italicRun!.RunProperties?.Italic);
    }

    private static DesignExportDto LoadSample(string fileName)
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "checklists", "word-fidelity-samples", fileName);
        Assert.True(File.Exists(path), $"Sample file missing: {path}");

        var json = File.ReadAllText(path);
        var design = JsonSerializer.Deserialize<DesignExportDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(design);
        return design!;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "PXA.sln")))
                return dir;

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}