using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PXA.Importer;
using PXA.Importer.Security;
using PXA.Importer.Trust;

namespace PXA.Importer.Tests;

public sealed class PdfTrustCorpusTests
{
    private static readonly string CorpusDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "PdfTrust");
    private static readonly string ExternalCorpusDirectory = Path.Combine(
        CorpusDirectory,
        "external");

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
    public async Task External_corpus_preserves_independent_producer_evidence_and_expected_reports()
    {
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(
            Path.Combine(ExternalCorpusDirectory, "external-corpus.json")))!.AsObject();
        Assert.Equal(1, manifest["schemaVersion"]!.GetValue<int>());
        var entries = manifest["entries"]!.AsArray();
        Assert.Equal(
            [
                "chromium-skia-tagged",
                "libreoffice-pdfa2b",
                "libreoffice-pdfua1",
                "libreoffice-tagged",
                "pyhanko-pades-bb",
                "qpdf-aes256-unicode",
            ],
            entries.Select(entry => entry!["id"]!.GetValue<string>()).Order(StringComparer.Ordinal));

        foreach (var item in entries)
        {
            var entry = item!.AsObject();
            var bytes = await File.ReadAllBytesAsync(Path.Combine(
                ExternalCorpusDirectory,
                entry["pdf"]!.GetValue<string>()));
            Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
            Assert.Equal(entry["fileSizeBytes"]!.GetValue<long>(), bytes.LongLength);
            Assert.Equal(
                entry["sha256"]!.GetValue<string>(),
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            Assert.Equal(
                await File.ReadAllBytesAsync(Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    entry["sourceCollection"]!.GetValue<string>(),
                    entry["sourceFixture"]!.GetValue<string>())),
                bytes);
            Assert.Contains(
                entry["producerMarker"]!.GetValue<string>(),
                System.Text.Encoding.Latin1.GetString(bytes),
                StringComparison.Ordinal);

            await using var stream = new MemoryStream(bytes, writable: false);
            var report = await new PdfTrustInspector().InspectAsync(stream);
            var expected = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(
                ExternalCorpusDirectory,
                entry["expectedReport"]!.GetValue<string>())));
            Assert.True(
                JsonNode.DeepEquals(expected, ProjectStableReport(report)),
                $"The expected trust report changed for external fixture '{entry["id"]}'.");

            if (entry["validation"] is JsonObject validation)
            {
                Assert.Equal("PASS", validation["outcome"]!.GetValue<string>());
                var evidenceBytes = await File.ReadAllBytesAsync(Path.Combine(
                    ExternalCorpusDirectory,
                    validation["evidence"]!.GetValue<string>()));
                Assert.Equal(
                    validation["sha256"]!.GetValue<string>(),
                    Convert.ToHexString(SHA256.HashData(evidenceBytes)).ToLowerInvariant());
                Assert.NotEmpty(evidenceBytes);
            }
        }

        Assert.Contains("AESv3", await File.ReadAllTextAsync(Path.Combine(
            ExternalCorpusDirectory,
            "qpdf-aes256-unicode-qpdf.txt")));
        Assert.Contains(":VALID", await File.ReadAllTextAsync(Path.Combine(
            ExternalCorpusDirectory,
            "pyhanko-pades-bb-validation.txt")));
        Assert.Contains("isCompliant=\"true\"", await File.ReadAllTextAsync(Path.Combine(
            ExternalCorpusDirectory,
            "libreoffice-pdfa2b-2b-verapdf.xml")));
        Assert.Contains("isCompliant=\"true\"", await File.ReadAllTextAsync(Path.Combine(
            ExternalCorpusDirectory,
            "libreoffice-pdfua1-ua1-verapdf.xml")));

        var encryptedBytes = await File.ReadAllBytesAsync(Path.Combine(
            ExternalCorpusDirectory,
            "qpdf-aes256-unicode.pdf"));
        await using var userStream = new MemoryStream(encryptedBytes, writable: false);
        var userDocument = await Pdf.LoadEncryptedAsync(userStream, "PXA-reader-ä漢字");
        Assert.Single(userDocument.Pages);
        await using var ownerStream = new MemoryStream(encryptedBytes, writable: false);
        var ownerDocument = await Pdf.LoadEncryptedAsync(ownerStream, "PXA-owner-ßЖ");
        Assert.Single(ownerDocument.Pages);
        await Assert.ThrowsAsync<PdfPasswordException>(async () =>
            await Pdf.LoadEncryptedAsync(
                new MemoryStream(encryptedBytes, writable: false),
                "wrong-password"));
    }

    [Fact]
    public async Task Pades_corpus_preserves_independent_profile_validation_evidence()
    {
        var directory = Path.Combine(CorpusDirectory, "pades");
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(
            Path.Combine(directory, "pades-corpus.json")))!.AsObject();
        Assert.Equal(1, manifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal("EU Digital Signature Services", manifest["validator"]!["name"]!.GetValue<string>());
        Assert.Equal("6.4", manifest["validator"]!["version"]!.GetValue<string>());

        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pxa-pades-b-t"] = "PAdES-BASELINE-T",
            ["pxa-pades-b-lt"] = "PAdES-BASELINE-LT",
            ["pxa-pades-b-lta"] = "PAdES-BASELINE-LTA",
        };
        var entries = manifest["entries"]!.AsArray();
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), entries
            .Select(entry => entry!["id"]!.GetValue<string>())
            .Order(StringComparer.Ordinal));

        foreach (var item in entries)
        {
            var entry = item!.AsObject();
            var id = entry["id"]!.GetValue<string>();
            Assert.Equal(expected[id], entry["expectedProfile"]!.GetValue<string>());
            Assert.Equal("TOTAL_PASSED", entry["expectedIndication"]!.GetValue<string>());

            var pdf = await AssertFileHashAsync(directory, entry["pdf"]!.GetValue<string>(), entry["sha256"]!.GetValue<string>());
            Assert.Equal(entry["fileSizeBytes"]!.GetValue<long>(), pdf.LongLength);
            Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));

            var reports = entry["reports"]!.AsObject();
            foreach (var kind in new[] { "simple", "detailed", "diagnostic" })
            {
                var report = reports[kind]!.AsObject();
                await AssertFileHashAsync(
                    directory,
                    report["file"]!.GetValue<string>(),
                    report["sha256"]!.GetValue<string>());
            }

            var simple = XDocument.Load(Path.Combine(directory, reports["simple"]!["file"]!.GetValue<string>()));
            var signature = simple.Descendants().First(element => element.Name.LocalName == "Signature");
            Assert.Equal(expected[id], signature.Attribute("SignatureFormat")?.Value);
            Assert.Equal(
                "TOTAL_PASSED",
                signature.Elements().Single(element => element.Name.LocalName == "Indication").Value);

            var diagnostic = XDocument.Load(Path.Combine(directory, reports["diagnostic"]!["file"]!.GetValue<string>()));
            Assert.Contains(diagnostic.Descendants(), element =>
                element.Name.LocalName == "SignatureFormat" && element.Value == expected[id]);
            Assert.Contains(diagnostic.Descendants(), element =>
                element.Name.LocalName == "StructuralValidation" && element.Attribute("valid")?.Value == "true");
        }

        var trustEvidence = manifest["trustEvidence"]!.AsObject();
        await AssertFileHashAsync(
            directory,
            trustEvidence["rootCertificate"]!.GetValue<string>(),
            trustEvidence["rootCertificateSha256"]!.GetValue<string>());
        await AssertFileHashAsync(
            directory,
            trustEvidence["crl"]!.GetValue<string>(),
            trustEvidence["crlSha256"]!.GetValue<string>());
    }

    [Fact]
    public async Task Corpus_contains_no_private_key_or_credential_artifacts()
    {
        var files = Directory.GetFiles(CorpusDirectory, "*", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "PdfTrustExternal"),
                "*",
                SearchOption.AllDirectories))
            .ToArray();
        Assert.All(files, path => Assert.Contains(
            Path.GetExtension(path).ToLowerInvariant(),
            new[] { ".cer", ".crl", ".json", ".md", ".html", ".pdf", ".txt", ".xml" }));
        foreach (var path in files.Where(path =>
                     Path.GetExtension(path) is ".json" or ".md" or ".html" or ".txt" or ".xml"))
        {
            var text = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("BEGIN PRIVATE KEY", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BEGIN ENCRYPTED PRIVATE KEY", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BEGIN CERTIFICATE", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<byte[]> AssertFileHashAsync(
        string directory,
        string file,
        string expectedSha256)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(directory, file));
        Assert.NotEmpty(bytes);
        Assert.Equal(expectedSha256, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        return bytes;
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
