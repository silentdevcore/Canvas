using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PVAD.Importer;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Saving;
using PVAD.Importer.Signing;
using PVAD.Importer.Trust;
using PVAD.Importer.Xref;
using PVAD.Pdf;

var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
var outputDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(repositoryRoot, "websites", "PVAD.Documentation", "public", "downloads", "pdf-trust");
Directory.CreateDirectory(outputDirectory);

var signingTime = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
var fixtures = new List<(string Id, string Title, byte[] Bytes)>
{
    ("clean", "Clean generated PDF", CreateCleanPdf()),
    ("malformed", "Malformed PDF with an unreferenced object", CreateMalformedPdf()),
    ("encrypted", "AES-256 encrypted PDF", CreateEncryptedPdf()),
    ("signed", "PAdES B-B signed PDF with a synthetic self-signed certificate", CreateSignedPdf(signingTime)),
    ("archived", "Generated PDF/A-2b document", CreateArchivedPdf(repositoryRoot)),
    ("accessible", "Generated PDF/UA-1 document", CreateAccessiblePdf()),
    ("redaction-risk", "PDF containing synthetic active-content and attachment risks", CreateRedactionRiskPdf()),
};

var manifestEntries = new JsonArray();
foreach (var fixture in fixtures)
{
    var pdfName = $"{fixture.Id}.pdf";
    var expectedName = $"{fixture.Id}.expected.json";
    var pdfPath = Path.Combine(outputDirectory, pdfName);
    await File.WriteAllBytesAsync(pdfPath, fixture.Bytes);

    await using var stream = new MemoryStream(fixture.Bytes, writable: false);
    var report = await new PdfTrustInspector().InspectAsync(stream);
    var expected = ProjectStableReport(report);
    await File.WriteAllTextAsync(
        Path.Combine(outputDirectory, expectedName),
        expected.ToJsonString(JsonOptions()) + Environment.NewLine,
        utf8WithoutBom);

    manifestEntries.Add(new JsonObject
    {
        ["id"] = fixture.Id,
        ["title"] = fixture.Title,
        ["pdf"] = pdfName,
        ["expectedReport"] = expectedName,
        ["sha256"] = Convert.ToHexString(SHA256.HashData(fixture.Bytes)).ToLowerInvariant(),
        ["fileSizeBytes"] = fixture.Bytes.LongLength,
    });
}

var manifest = new JsonObject
{
    ["schemaVersion"] = 1,
    ["description"] = "Synthetic PDF Trust examples. No customer files, production certificates, private keys, or operational credentials are included.",
    ["entries"] = manifestEntries,
};
await File.WriteAllTextAsync(
    Path.Combine(outputDirectory, "corpus.json"),
    manifest.ToJsonString(JsonOptions()) + Environment.NewLine,
    utf8WithoutBom);

Console.WriteLine($"Wrote {fixtures.Count} PDF Trust fixtures to {outputDirectory}.");

static byte[] CreateCleanPdf()
{
    var document = CreateDocument("Clean PDF Trust example");
    document.AddPage().DrawText("Synthetic clean PDF", 40, 40);
    return document.ToBytes();
}

static byte[] CreateMalformedPdf()
{
    var source = CreateCleanPdf();
    return source.Concat(Encoding.ASCII.GetBytes(
        "\n999 0 obj\n<< /Type /Example /Value (unreferenced synthetic object) >>\nendobj\n")).ToArray();
}

static byte[] CreateEncryptedPdf()
{
    var document = CreateDocument("Encrypted PDF Trust example");
    document.AddPage().DrawText("Synthetic encrypted PDF", 40, 40);
    return document.ToBytes(new PdfSaveOptions
    {
        Encryption = new PdfEncryptionOptions
        {
            UserPassword = "synthetic-reader-password",
            OwnerPassword = "synthetic-owner-password",
            Algorithm = PdfEncryptionAlgorithm.Aes256,
            Permissions = PdfPermissions.Print | PdfPermissions.ExtractForAccessibility,
        },
    });
}

static byte[] CreateSignedPdf(DateTimeOffset signingTime)
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest(
        "CN=PXA Synthetic Corpus Signer",
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509KeyUsageExtension(
        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
        critical: true));
    using var certificate = request.CreateSelfSigned(signingTime.AddDays(-1), signingTime.AddDays(30));
    var pfx = certificate.Export(X509ContentType.Pkcs12, "synthetic-build-only");
    using var key = new PdfPkcs12SigningKey(pfx, "synthetic-build-only");
    CryptographicOperations.ZeroMemory(pfx);

    var document = CreateDocument("Signed PDF Trust example");
    document.AddPage().DrawText("Synthetic PAdES B-B signature", 40, 40);
    return new PdfPadesSigningService().Sign(
        document.ToBytes(),
        key,
        new PdfSigningOptions
        {
            FieldName = "SyntheticApproval",
            Reason = "PDF Trust corpus verification",
            Location = "Zurich",
            SigningTime = signingTime,
            Profile = PdfPadesProfile.BaselineB,
        }).Bytes;
}

static byte[] CreateArchivedPdf(string repositoryRoot)
{
    var document = CreateDocument("Archived PDF Trust example");
    document.FontLoader = new PdfFontLoader(Path.Combine(repositoryRoot, "assets", "fonts", "liberation"));
    document.Language = "en-US";
    document.AddPage().DrawText(
        "Synthetic PDF/A-2b document",
        40,
        40,
        new PdfDrawTextOptions { Language = "en-US" });
    return document.ToBytes(new PdfSaveOptions { ComplianceProfile = PdfComplianceProfile.PdfA2B });
}

static byte[] CreateAccessiblePdf()
{
    var document = CreateDocument("Accessible PDF Trust example");
    document.Language = "en-US";
    var page = document.AddPage();
    page.DrawRectangle(40, 740, 220, 30);
    page.TagLastElement(new PdfAccessibilityProperties
    {
        Role = PdfStructureRole.H1,
        ElementId = "title",
        ActualText = "Synthetic accessible document",
        Language = "en-US",
        ReadingOrder = 1,
    });
    page.DrawRectangle(40, 690, 360, 24);
    page.TagLastElement(new PdfAccessibilityProperties
    {
        Role = PdfStructureRole.P,
        ElementId = "summary",
        ActualText = "A tagged paragraph for PDF Trust verification.",
        Language = "en-US",
        ReadingOrder = 2,
    });
    page.DrawLine(40, 670, 400, 670);
    page.MarkLastElementAsArtifact();
    return document.ToBytes(new PdfSaveOptions { AccessibilityProfile = PdfAccessibilityProfile.PdfUa1 });
}

static byte[] CreateRedactionRiskPdf()
{
    var document = CreateDocument("Redaction-risk PDF Trust example");
    document.AddPage().DrawText("Synthetic active-content inventory", 40, 40);
    var source = document.ToBytes();
    var nextObject = NextObjectNumber(source);
    return new PdfIncrementalUpdateWriter().Append(source,
    [
        new PdfIncrementalObjectUpdate(new PdfObjectId(nextObject, 0), new PdfDictionary
        {
            ["Type"] = new PdfName("Action"),
            ["S"] = new PdfName("JavaScript"),
            ["JS"] = new PdfString("app.alert('synthetic')"u8.ToArray(), IsHex: false),
        }),
        new PdfIncrementalObjectUpdate(new PdfObjectId(nextObject + 1, 0), new PdfDictionary
        {
            ["Type"] = new PdfName("Filespec"),
            ["F"] = new PdfString("synthetic-evidence.txt"u8.ToArray(), IsHex: false),
            ["EF"] = new PdfDictionary(),
        }),
    ]).Bytes;
}

static PdfDocument CreateDocument(string title)
{
#pragma warning disable PXA0001
    var document = new PdfDocument();
#pragma warning restore PXA0001
    document.Info.Title = title;
    document.Info.Author = "PXA synthetic test corpus";
    document.Info.CreationDate = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    document.Info.ModificationDate = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    return document;
}

static int NextObjectNumber(byte[] source)
{
    var context = new PdfParseContext(source, new PdfImporterOptions());
    var table = new PdfCrossReferenceParser().Parse(context);
    return table.Entries.Keys.Max(static id => id.Number) + 1;
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

static JsonSerializerOptions JsonOptions()
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    options.Converters.Add(new JsonStringEnumConverter());
    return options;
}

static string FindRepositoryRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln")))
        directory = directory.Parent;
    return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the PXA repository root.");
}
