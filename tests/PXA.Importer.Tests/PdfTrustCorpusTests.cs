using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PXA.Importer.Trust;

namespace PXA.Importer.Tests;

public sealed class PdfTrustCorpusTests
{
    private static readonly string CorpusDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "PdfTrust");

    [Fact]
    public async Task Checked_in_corpus_matches_hashes_and_expected_trust_reports()
    {
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(
            Path.Combine(CorpusDirectory, "corpus.json")))!.AsObject();
        Assert.Equal(1, manifest["schemaVersion"]!.GetValue<int>());
        var entries = manifest["entries"]!.AsArray();
        Assert.Equal(
            ["accessible", "archived", "clean", "encrypted", "malformed", "redaction-risk", "signed"],
            entries.Select(entry => entry!["id"]!.GetValue<string>()).Order(StringComparer.Ordinal));

        foreach (var item in entries)
        {
            var entry = item!.AsObject();
            var pdfPath = Path.Combine(CorpusDirectory, entry["pdf"]!.GetValue<string>());
            var expectedPath = Path.Combine(CorpusDirectory, entry["expectedReport"]!.GetValue<string>());
            var bytes = await File.ReadAllBytesAsync(pdfPath);
            Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
            Assert.Equal(entry["fileSizeBytes"]!.GetValue<long>(), bytes.LongLength);
            Assert.Equal(
                entry["sha256"]!.GetValue<string>(),
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());

            await using var stream = new MemoryStream(bytes, writable: false);
            var report = await new PdfTrustInspector().InspectAsync(stream);
            var expected = JsonNode.Parse(await File.ReadAllTextAsync(expectedPath));
            Assert.True(
                JsonNode.DeepEquals(expected, ProjectStableReport(report)),
                $"The expected trust report changed for '{entry["id"]}'.");
        }
    }

    [Fact]
    public async Task Corpus_contains_no_private_key_or_credential_artifacts()
    {
        var files = Directory.GetFiles(CorpusDirectory);
        Assert.All(files, path => Assert.Contains(
            Path.GetExtension(path).ToLowerInvariant(),
            new[] { ".json", ".md", ".html", ".pdf" }));
        foreach (var path in files.Where(path => Path.GetExtension(path) is ".json" or ".md" or ".html"))
        {
            var text = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("BEGIN PRIVATE KEY", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BEGIN ENCRYPTED PRIVATE KEY", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BEGIN CERTIFICATE", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static JsonObject ProjectStableReport(PdfTrustReport report) => new()
    {
        ["status"] = report.Status.ToString(),
        ["structureStatus"] = report.StructureStatus.ToString(),
        ["pdfVersion"] = report.PdfVersion,
        ["revisionCount"] = report.RevisionCount,
        ["objectCount"] = report.ObjectCount,
        ["encryption"] = new JsonObject
        {
            ["isEncrypted"] = report.Encryption.IsEncrypted,
            ["status"] = report.Encryption.Status.ToString(),
            ["filter"] = report.Encryption.Filter,
            ["algorithm"] = report.Encryption.Algorithm,
            ["version"] = report.Encryption.Version,
            ["revision"] = report.Encryption.Revision,
            ["keyLengthBits"] = report.Encryption.KeyLengthBits,
        },
        ["signatures"] = new JsonArray(report.Signatures.Select(signature => (JsonNode)new JsonObject
        {
            ["status"] = signature.Status.ToString(),
            ["subFilter"] = signature.SubFilter,
            ["integrityStatus"] = signature.IntegrityStatus.ToString(),
            ["coverageStatus"] = signature.CoverageStatus.ToString(),
            ["certificateStatus"] = signature.CertificateStatus.ToString(),
            ["padesProfile"] = signature.PadesProfile,
            ["digestAlgorithm"] = signature.DigestAlgorithm,
            ["coversWholeDocument"] = signature.CoversWholeDocument,
            ["isDocumentTimestamp"] = signature.IsDocumentTimestamp,
        }).ToArray()),
        ["pdfA"] = ProjectClaim(report.PdfA),
        ["pdfUa"] = ProjectClaim(report.PdfUa),
        ["risk"] = report.Risk is null ? null : new JsonObject
        {
            ["javaScriptActionCount"] = report.Risk.JavaScriptActionCount,
            ["launchActionCount"] = report.Risk.LaunchActionCount,
            ["embeddedFileCount"] = report.Risk.EmbeddedFileCount,
            ["fileAttachmentCount"] = report.Risk.FileAttachmentCount,
            ["richMediaCount"] = report.Risk.RichMediaCount,
            ["xfaFormCount"] = report.Risk.XfaFormCount,
            ["externalLinkCount"] = report.Risk.ExternalLinkCount,
            ["status"] = report.Risk.Status.ToString(),
            ["hasRiskyContent"] = report.Risk.HasRiskyContent,
        },
        ["diagnosticCodes"] = new JsonArray(report.Diagnostics
            .Select(static diagnostic => diagnostic.Code)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(static code => (JsonNode)JsonValue.Create(code)!)
            .ToArray()),
    };

    private static JsonObject ProjectClaim(PdfComplianceClaim claim) => new()
    {
        ["standard"] = claim.Standard,
        ["profile"] = claim.Profile,
        ["isClaimed"] = claim.IsClaimed,
        ["status"] = claim.Status.ToString(),
        ["evidence"] = claim.Evidence,
        ["validator"] = claim.Validator,
    };
}
