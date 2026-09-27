using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PXA.Generator;

namespace PXA.Generator.Tests;

/// <summary>
/// EINV-07: the maintained fixture corpus and its manifest.
///
/// Every fixture is checked three ways: its bytes still hash to the recorded SHA-256, its
/// provenance and licence are recorded, and recognition produces exactly the outcome the manifest
/// declares. The hash check is a gate, not a self-healing step - a fixture that changes fails here
/// rather than quietly redefining what the corpus proves.
/// </summary>
public sealed class EInvoiceFixtureCorpusTests
{
    public static TheoryData<string> FixtureIds()
    {
        var data = new TheoryData<string>();
        foreach (var fixture in Manifest.Fixtures)
        {
            data.Add(fixture.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(FixtureIds))]
    public void Fixture_bytes_still_match_the_recorded_checksum(string id)
    {
        var fixture = Manifest.Fixtures.Single(item => item.Id == id);
        var bytes = File.ReadAllBytes(Path.Combine(CorpusDirectory, fixture.Path));

        Assert.Equal(fixture.SizeBytes, bytes.Length);
        Assert.Equal(fixture.Sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Theory]
    [MemberData(nameof(FixtureIds))]
    public void Fixture_recognition_matches_its_declared_expectation(string id)
    {
        var fixture = Manifest.Fixtures.Single(item => item.Id == id);
        var bytes = File.ReadAllBytes(Path.Combine(CorpusDirectory, fixture.Path));

        var recognition = EInvoice.Recognize(bytes);
        var errors = recognition.Diagnostics.Where(item => item.Severity == "error").ToArray();
        var warnings = recognition.Diagnostics.Where(item => item.Severity == "warning").ToArray();

        switch (fixture.ExpectedResult)
        {
            case "recognized":
                Assert.True(
                    recognition.IsRecognized,
                    $"{id} should be recognized but reported: " +
                        string.Join("; ", errors.Select(item => item.Code + " " + item.Message)));
                Assert.Empty(errors);
                Assert.Equal(fixture.Syntax, recognition.Syntax);
                Assert.Equal(fixture.Profile, recognition.Profile);
                break;

            case "recognizedWithWarnings":
                Assert.True(recognition.IsRecognized, $"{id} should still be recognized.");
                Assert.Empty(errors);
                foreach (var code in fixture.ExpectedDiagnosticCodes)
                {
                    Assert.Contains(code, warnings.Select(item => item.Code));
                }
                break;

            case "rejected":
                Assert.False(recognition.IsRecognized, $"{id} must fail closed.");
                Assert.NotEmpty(errors);
                foreach (var code in fixture.ExpectedDiagnosticCodes)
                {
                    Assert.Contains(code, errors.Select(item => item.Code));
                }
                break;

            default:
                Assert.Fail($"{id} declares an unknown expected result '{fixture.ExpectedResult}'.");
                break;
        }
    }

    [Fact]
    public void Every_fixture_records_its_provenance_and_licence()
    {
        Assert.Equal("pxa.e-invoice.fixture-corpus/1", Manifest.Schema);
        foreach (var fixture in Manifest.Fixtures)
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.License), $"{fixture.Id} needs a licence.");
            Assert.False(string.IsNullOrWhiteSpace(fixture.Description), $"{fixture.Id} needs a description.");
            Assert.Contains(fixture.Provenance, Manifest.ProvenanceKinds.Keys);
            Assert.Contains(fixture.ExpectedResult, Manifest.ExpectedResults.Keys);
            Assert.NotEmpty(fixture.Tags);
        }
    }

    /// <summary>
    /// A customer invoice must never enter the corpus, and an official artifact may only enter it
    /// after its redistribution terms have been reviewed and recorded (EINV-01).
    /// </summary>
    [Fact]
    public void No_fixture_is_customer_data_or_an_unreviewed_official_artifact()
    {
        Assert.DoesNotContain("customer", Manifest.Fixtures.Select(item => item.Provenance));
        Assert.DoesNotContain(
            "official-licensed",
            Manifest.Fixtures.Select(item => item.Provenance));
        foreach (var fixture in Manifest.Fixtures)
        {
            Assert.Equal("pxa-authored", fixture.Provenance);
        }
    }

    [Fact]
    public void No_fixture_claims_official_conformance()
    {
        foreach (var fixture in Manifest.Fixtures)
        {
            Assert.NotEqual("validated", fixture.ExpectedResult);
            Assert.DoesNotContain("conformant", fixture.Description, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("never a conformance claim", Manifest.ExpectedResults["recognized"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_manifest_and_the_directory_do_not_drift_apart()
    {
        var declared = Manifest.Fixtures.Select(item => item.Path).Order(StringComparer.Ordinal).ToArray();
        var present = Directory
            .EnumerateFiles(CorpusDirectory, "*.xml")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(declared, present);
        Assert.Equal(
            Manifest.Fixtures.Count,
            Manifest.Fixtures.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Manifest.Fixtures.Count,
            Manifest.Fixtures.Select(item => item.Sha256).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The categories the corpus exists to cover; losing one silently would be a regression.</summary>
    [Theory]
    [InlineData("multilingual")]
    [InlineData("multiple-tax-rates")]
    [InlineData("rounding")]
    [InlineData("large-line-count")]
    [InlineData("credit-note")]
    [InlineData("malformed")]
    [InlineData("adversarial")]
    [InlineData("ubl")]
    [InlineData("zero-rated")]
    public void The_corpus_keeps_covering_its_required_categories(string tag)
    {
        Assert.Contains(Manifest.Fixtures, fixture => fixture.Tags.Contains(tag));
    }

    [Fact]
    public void Both_maintained_syntaxes_and_all_three_standards_are_represented()
    {
        var standards = Manifest.Fixtures
            .Where(item => item.Standard is not null)
            .Select(item => item.Standard!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var syntaxes = Manifest.Fixtures
            .Where(item => item.Syntax is not null)
            .Select(item => item.Syntax!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("FacturX", standards);
        Assert.Contains("XRechnung", standards);
        Assert.Contains("CII", syntaxes);
        Assert.Contains("UBL-Invoice", syntaxes);
        Assert.Contains("UBL-CreditNote", syntaxes);
    }

    [Fact]
    public void Every_adversarial_fixture_fails_closed_without_resolving_anything()
    {
        foreach (var fixture in Manifest.Fixtures.Where(item => item.Tags.Contains("adversarial")))
        {
            var bytes = File.ReadAllBytes(Path.Combine(CorpusDirectory, fixture.Path));
            var recognition = EInvoice.Recognize(bytes);

            Assert.False(recognition.IsRecognized);
            Assert.Equal("rejected", fixture.ExpectedResult);
            // No entity content may appear in any reported message.
            foreach (var diagnostic in recognition.Diagnostics)
            {
                Assert.DoesNotContain("expanded", diagnostic.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("root:x:", diagnostic.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("aaaaaaaaaa", diagnostic.Message, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void The_large_fixture_stays_within_the_bounded_input_limit()
    {
        var fixture = Manifest.Fixtures.Single(item => item.Id == "factur-x-large-500-lines");
        var bytes = File.ReadAllBytes(Path.Combine(CorpusDirectory, fixture.Path));

        Assert.True(bytes.Length < EInvoice.MaximumXmlBytes);
        var recognition = EInvoice.Recognize(bytes);
        Assert.True(recognition.IsRecognized);
        Assert.Equal(500, recognition.SemanticData!.Lines.Count);
        Assert.Equal(5000.00m, recognition.SemanticData.LineNetTotal);
        Assert.Equal(5950.00m, recognition.SemanticData.PayableAmount);
    }

    [Fact]
    public void The_multilingual_fixture_survives_extraction_unchanged()
    {
        var fixture = Manifest.Fixtures.Single(item => item.Id == "factur-x-multilingual");
        var bytes = File.ReadAllBytes(Path.Combine(CorpusDirectory, fixture.Path));

        var semantic = EInvoice.Recognize(bytes).SemanticData;

        Assert.NotNull(semantic);
        Assert.Equal("Groß & Söhne GmbH", semantic!.Seller.Name);
        Assert.Equal("Société Générale de Facturation", semantic.Buyer.Name);
        Assert.Contains(semantic.Lines, line => line.Description == "خدمات استشارية");
        Assert.Contains(semantic.Lines, line => line.Description == "日本語のサービス");
        Assert.Contains(semantic.Lines, line => line.Description == "Beratungsleistung Ökosteuer");
    }

    private static string CorpusDirectory { get; } = ResolveCorpusDirectory();

    private static FixtureManifest Manifest { get; } = JsonSerializer.Deserialize<FixtureManifest>(
        File.ReadAllText(Path.Combine(CorpusDirectory, "manifest.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static string ResolveCorpusDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "test-assets", "e-invoicing", "corpus");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("test-assets/e-invoicing/corpus was not found.");
    }

    private sealed record FixtureManifest(
        string Schema,
        [property: JsonPropertyName("provenanceKinds")] IReadOnlyDictionary<string, string> ProvenanceKinds,
        [property: JsonPropertyName("expectedResults")] IReadOnlyDictionary<string, string> ExpectedResults,
        IReadOnlyList<CorpusFixture> Fixtures);

    private sealed record CorpusFixture(
        string Id,
        string Path,
        string Sha256,
        int SizeBytes,
        string Provenance,
        string License,
        string? Standard,
        string? StandardVersion,
        string? Profile,
        string? Syntax,
        string ExpectedResult,
        IReadOnlyList<string> ExpectedDiagnosticCodes,
        IReadOnlyList<string> Tags,
        string Description);
}
