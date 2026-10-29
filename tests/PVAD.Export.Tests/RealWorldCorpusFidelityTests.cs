using System.Security.Cryptography;
using System.Text.Json;
using PVAD.FileImporter;
using PVAD.Infrastructure.Spreadsheet;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Export.Tests;

[Collection(DocumentFidelityCollection.Name)]
public sealed class RealWorldCorpusFidelityTests
{
    private sealed record CorpusFixture(
        string FixtureId,
        string Path,
        string SourceUrl,
        string LandingPageUrl,
        string Publisher,
        string Attribution,
        string Sha256);

    private sealed record CorpusLicense(string Name, string Url, string Redistribution, string Notes);

    private sealed record CorpusSources(int SchemaVersion, string RetrievedAt, CorpusLicense License, IReadOnlyList<CorpusFixture> Fixtures);

    private sealed record CorpusResult(
        string FixtureId,
        string Domain,
        string Workflow,
        string StructuralUnit,
        int ExpectedUnits,
        int ImportedUnits,
        int ImportedElements,
        int DiagnosticCount,
        bool ImportSucceeded,
        IReadOnlyList<string> Limitations);

    private sealed record CorpusReport(int SchemaVersion, IReadOnlyList<CorpusResult> Fixtures);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [Fact]
    public async Task PublicRealWorldCorpus_HasImmutableSourcesAndImportsThroughProductionAdapters()
    {
        var root = FindRepoRoot();
        var metadataPath = Path.Combine(root, "test-assets", "document-fidelity", "real-world", "corpus-sources.json");
        var sources = JsonSerializer.Deserialize<CorpusSources>(File.ReadAllText(metadataPath), JsonOptions);
        Assert.NotNull(sources);
        Assert.Equal(1, sources!.SchemaVersion);
        Assert.Equal(3, sources.Fixtures.Count);
        Assert.Equal("Open Government Licence v3.0", sources.License.Name);
        Assert.Equal("https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/", sources.License.Url);
        Assert.Equal("allowed-with-attribution", sources.License.Redistribution);
        Assert.False(string.IsNullOrWhiteSpace(sources.License.Notes));

        var expectedPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["real-world-pdf-ukgi-annual-report"] = "test-assets/document-fidelity/real-world/ukgi-annual-report-2019.pdf",
            ["real-world-docx-suffolk-mapping-guidance"] = "test-assets/document-fidelity/real-world/suffolk-public-mapping-guidance.docx",
            ["real-world-xlsx-suffolk-buildings"] = "test-assets/document-fidelity/real-world/suffolk-buildings-2026.xlsx"
        };
        Assert.Equal(expectedPaths.Keys.Order(), sources.Fixtures.Select(fixture => fixture.FixtureId).Order());

        foreach (var fixture in sources.Fixtures)
        {
            Assert.Equal(expectedPaths[fixture.FixtureId], fixture.Path);
            var fullPath = Path.Combine(root, fixture.Path.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"Missing corpus fixture {fixture.Path}.");
            Assert.Equal(fixture.Sha256, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(fullPath))));
            Assert.StartsWith("https://", fixture.SourceUrl, StringComparison.Ordinal);
            Assert.StartsWith("https://", fixture.LandingPageUrl, StringComparison.Ordinal);
            Assert.Contains("Open Government Licence v3.0", fixture.Attribution, StringComparison.Ordinal);
        }

        var output = PVAD.Testing.PvadTestEnvironment.Read("PVAD_REAL_WORLD_FIDELITY_ARTIFACTS") ??
            Path.Combine(root, "artifacts", "document-fidelity", "real-world", "raw");
        var generated = Path.Combine(output, "generated");
        Directory.CreateDirectory(generated);

        var results = new List<CorpusResult>
        {
            await ImportPdf(root, generated),
            ImportDocx(root, generated),
            ImportXlsx(root, generated)
        };

        Assert.All(results, result => Assert.True(result.ImportSucceeded));
        Assert.All(results, result => Assert.True(result.ImportedUnits >= result.ExpectedUnits));
        Assert.All(results, result => Assert.True(result.ImportedElements > 0));

        Directory.CreateDirectory(output);
        File.WriteAllText(
            Path.Combine(output, "fidelity-report.json"),
            JsonSerializer.Serialize(new CorpusReport(1, results), JsonOptions));
    }

    private static async Task<CorpusResult> ImportPdf(string root, string generated)
    {
        var path = FixturePath(root, "ukgi-annual-report-2019.pdf");
        await using var stream = File.OpenRead(path);
        var design = await new PdfFileImporter().ImportAsync(stream, "UKGI Annual Report 2018-19");
        var rendered = DesignJsonMapper.MapToPdfDocument(design).ToBytes();
        AssertPdf(rendered, "PDF roundtrip");
        File.WriteAllBytes(Path.Combine(generated, "real-world-pdf-ukgi-annual-report.pdf"), rendered);
        return new CorpusResult(
            "real-world-pdf-ukgi-annual-report", "pdf", "import-roundtrip", "page", 62,
            design.Pages.Count, CountElements(design), design.ImportDiagnostics?.Count ?? 0, true,
            ["Import success and structural page count are measured; visual and semantic fidelity still require human review."]);
    }

    private static CorpusResult ImportDocx(string root, string generated)
    {
        using var stream = File.OpenRead(FixturePath(root, "suffolk-public-mapping-guidance.docx"));
        var design = DocxFileImporter.Import(stream, "Suffolk public mapping guidance");
        var rendered = DesignJsonMapper.MapToPdfDocument(design).ToBytes();
        AssertPdf(rendered, "DOCX roundtrip");
        File.WriteAllBytes(Path.Combine(generated, "real-world-docx-suffolk-mapping-guidance.pdf"), rendered);
        return new CorpusResult(
            "real-world-docx-suffolk-mapping-guidance", "word", "import-roundtrip", "document", 1,
            design.Pages.Count > 0 ? 1 : 0, CountElements(design), design.ImportDiagnostics?.Count ?? 0, true,
            ["The current DOCX importer models sections rather than physical pagination; visual and semantic fidelity still require human review."]);
    }

    private static CorpusResult ImportXlsx(string root, string generated)
    {
        using var stream = File.OpenRead(FixturePath(root, "suffolk-buildings-2026.xlsx"));
        var workbook = new ExcelWorkbookImporter().Import(stream, "Suffolk buildings 2026");
        var rendered = new ExcelWorkbookExporter().Export(workbook);
        Assert.True(rendered.Length > 4);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(rendered, 0, 2));
        File.WriteAllBytes(Path.Combine(generated, "real-world-xlsx-suffolk-buildings.xlsx"), rendered);
        return new CorpusResult(
            "real-world-xlsx-suffolk-buildings", "spreadsheet", "import-roundtrip", "sheet", 1,
            workbook.Sheets.Count, workbook.Sheets.Sum(sheet => sheet.Cells.Count), 0, true,
            ["Import success and sheet count are measured; formula, style, print, and visual fidelity still require human review."]);
    }

    private static string FixturePath(string root, string name) =>
        Path.Combine(root, "test-assets", "document-fidelity", "real-world", name);

    private static int CountElements(DesignExportDto design) =>
        design.Pages.Sum(page => page.Elements.Count) + design.SharedElements.Count;

    private static void AssertPdf(byte[] bytes, string label)
    {
        Assert.True(bytes.Length > 5, $"{label} was empty.");
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find PXA repository root.");
    }
}
