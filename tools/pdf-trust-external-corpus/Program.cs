using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PVAD.Importer.Trust;

var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var outputDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(
        repositoryRoot,
        "websites",
        "PVAD.Documentation",
        "public",
        "downloads",
        "pdf-trust",
        "external");
Directory.CreateDirectory(outputDirectory);

var fixtures = new[]
{
    new ExternalFixture(
        "chromium-skia-tagged",
        "Chromium/Skia tagged text PDF",
        "Chromium with Skia/PDF",
        "152",
        "Skia/PDF m152",
        Path.Combine(
            repositoryRoot,
            "tests",
            "PVAD.Importer.Tests",
            "Fixtures",
            "RedactionRecovery",
            "chromium-encoded-glyphs.pdf")),
    new ExternalFixture(
        "libreoffice-tagged",
        "LibreOffice tagged text and reusable resources PDF",
        "LibreOffice",
        "26.2.3.2",
        "LibreOffice 26.2.3.2",
        Path.Combine(
            repositoryRoot,
            "tests",
            "PVAD.Importer.Tests",
            "Fixtures",
            "RedactionRecovery",
            "libreoffice-reusable-resources.pdf")),
    new ExternalFixture(
        "qpdf-aes256-unicode",
        "qpdf AES-256/R6 PDF with Unicode passwords",
        "qpdf",
        "12.2.0",
        "/CFM /AESV3",
        Path.Combine(repositoryRoot, "tests", "PVAD.Importer.Tests", "Fixtures", "PdfTrustExternal", "qpdf-aes256-unicode.pdf"),
        "qpdf 12.2.0",
        "PASS",
        Path.Combine(repositoryRoot, "tests", "PVAD.Importer.Tests", "Fixtures", "PdfTrustExternal", "qpdf-aes256-unicode-qpdf.txt")),
    new ExternalFixture(
        "pyhanko-pades-bb",
        "pyHanko PAdES Baseline B-B signed PDF",
        "pyHanko CLI",
        "0.37.0",
        "/Name /pyHanko#20CLI",
        Path.Combine(repositoryRoot, "tests", "PVAD.Importer.Tests", "Fixtures", "PdfTrustExternal", "pyhanko-pades-bb.pdf"),
        "pyHanko 0.37.0",
        "PASS",
        Path.Combine(repositoryRoot, "tests", "PVAD.Importer.Tests", "Fixtures", "PdfTrustExternal", "pyhanko-pades-bb-validation.txt")),
    new ExternalFixture(
        "libreoffice-pdfa2b",
        "LibreOffice PDF/A-2b document",
        "LibreOffice",
        "26.2.3.2",
        "LibreOffice 26.2.3.2",
        Path.Combine(repositoryRoot, "tests", "PVAD.Importer.Tests", "Fixtures", "PdfTrustExternal", "libreoffice-pdfa2b.pdf"),
        "veraPDF CLI 1.31.166",
        "PASS",
        Path.Combine(repositoryRoot, "tests", "PVAD.Importer.Tests", "Fixtures", "PdfTrustExternal", "libreoffice-pdfa2b-2b-verapdf.xml")),
    new ExternalFixture(
        "libreoffice-pdfua1",
        "LibreOffice PDF/UA-1 document",
        "LibreOffice",
        "26.2.3.2",
        "LibreOffice 26.2.3.2",
        Path.Combine(repositoryRoot, "tests", "PVAD.Importer.Tests", "Fixtures", "PdfTrustExternal", "libreoffice-pdfua1.pdf"),
        "veraPDF CLI 1.31.166",
        "PASS",
        Path.Combine(repositoryRoot, "tests", "PVAD.Importer.Tests", "Fixtures", "PdfTrustExternal", "libreoffice-pdfua1-ua1-verapdf.xml")),
};

var entries = new JsonArray();
foreach (var fixture in fixtures)
{
    var bytes = await File.ReadAllBytesAsync(fixture.SourcePath);
    var pdfText = Encoding.Latin1.GetString(bytes);
    if (!pdfText.Contains(fixture.ProducerMarker, StringComparison.Ordinal))
        throw new InvalidDataException($"Producer marker is missing for '{fixture.Id}'.");

    var pdfName = $"{fixture.Id}.pdf";
    var expectedName = $"{fixture.Id}.expected.json";
    await File.WriteAllBytesAsync(Path.Combine(outputDirectory, pdfName), bytes);

    await using var stream = new MemoryStream(bytes, writable: false);
    var report = await new PdfTrustInspector().InspectAsync(stream);
    await WriteJsonAsync(
        Path.Combine(outputDirectory, expectedName),
        ProjectStableReport(report));

    var entry = new JsonObject
    {
        ["id"] = fixture.Id,
        ["title"] = fixture.Title,
        ["producer"] = fixture.Producer,
        ["producerVersion"] = fixture.ProducerVersion,
        ["producerMarker"] = fixture.ProducerMarker,
        ["origin"] = "Locally generated synthetic fixture produced by independent software",
        ["sourceCollection"] = fixture.SourcePath.Contains("PdfTrustExternal", StringComparison.Ordinal)
            ? "PdfTrustExternal"
            : "RedactionRecovery",
        ["sourceFixture"] = Path.GetFileName(fixture.SourcePath),
        ["pdf"] = pdfName,
        ["expectedReport"] = expectedName,
        ["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
        ["fileSizeBytes"] = bytes.LongLength,
    };

    if (fixture.ValidationEvidencePath is not null)
    {
        var evidenceBytes = await File.ReadAllBytesAsync(fixture.ValidationEvidencePath);
        var evidenceName = Path.GetFileName(fixture.ValidationEvidencePath);
        await File.WriteAllBytesAsync(Path.Combine(outputDirectory, evidenceName), evidenceBytes);
        entry["validation"] = new JsonObject
        {
            ["tool"] = fixture.ValidationTool,
            ["outcome"] = fixture.ValidationOutcome,
            ["evidence"] = evidenceName,
            ["sha256"] = Convert.ToHexString(SHA256.HashData(evidenceBytes)).ToLowerInvariant(),
        };
    }

    entries.Add(entry);
}

await WriteJsonAsync(
    Path.Combine(outputDirectory, "external-corpus.json"),
    new JsonObject
    {
        ["schemaVersion"] = 1,
        ["description"] = "Synthetic PDFs created by independent desktop and browser PDF producers. No third-party document content is included.",
        ["entries"] = entries,
    });

Console.WriteLine($"Wrote {fixtures.Length} external PDF Trust fixtures to {outputDirectory}.");

static async Task WriteJsonAsync(string path, JsonNode value)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    await File.WriteAllTextAsync(
        path,
        value.ToJsonString(options) + Environment.NewLine,
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static JsonObject ProjectStableReport(PdfTrustReport report) => new()
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

static JsonObject ProjectClaim(PdfComplianceClaim claim) => new()
{
    ["standard"] = claim.Standard,
    ["profile"] = claim.Profile,
    ["isClaimed"] = claim.IsClaimed,
    ["status"] = claim.Status.ToString(),
    ["evidence"] = claim.Evidence,
    ["validator"] = claim.Validator,
};

static string FindRepositoryRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln")))
        directory = directory.Parent;
    return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the PVAD repository root.");
}

internal sealed record ExternalFixture(
    string Id,
    string Title,
    string Producer,
    string ProducerVersion,
    string ProducerMarker,
    string SourcePath,
    string? ValidationTool = null,
    string? ValidationOutcome = null,
    string? ValidationEvidencePath = null);
