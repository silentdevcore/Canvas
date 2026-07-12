using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using PXA.Infrastructure.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace PXA.Export.Tests;

/// <summary>
/// Covers the three hardening fixes: document-protection password hashing,
/// SSRF-guarded remote image fetch, and C14N-correct digital signatures.
/// </summary>
public sealed class WordSecurityTests
{
    private static DesignExportDto SingleTextDesign(PageSettingsDto? settings = null) => new()
    {
        Id = "sec",
        Name = "Security",
        PageSettings = settings,
        Pages =
        [
            new PageDto
            {
                Id = "p1",
                Elements =
                [
                    new ElementDto { Id = "t1", Type = "text", X = 10, Y = 10, Width = 100, Height = 20, Content = "Hello" },
                ],
            },
        ],
    };

    // ── Document protection ──────────────────────────────────────────────────

    [Fact]
    public void Protection_WithPassword_WritesSaltedIteratedSha1Hash()
    {
        var design = SingleTextDesign(new PageSettingsDto
        {
            Protection = new DocumentProtectionDto { Enabled = true, Mode = "readOnly", PasswordHash = "s3cret" },
        });

        var bytes = new WordDocumentExporter().Export(design);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var dp = doc.MainDocumentPart!.DocumentSettingsPart!.Settings!.GetFirstChild<DocumentProtection>();

        Assert.NotNull(dp);
        Assert.Equal(DocumentProtectionValues.ReadOnly, dp!.Edit!.Value);
        Assert.True(dp.Enforcement!.Value);
        Assert.Equal(4, dp.CryptographicAlgorithmSid!.Value);          // SHA-1
        Assert.Equal(50_000u, dp.CryptographicSpinCount!.Value);
        Assert.False(string.IsNullOrWhiteSpace(dp.Hash));
        Assert.False(string.IsNullOrWhiteSpace(dp.Salt));

        // Hash must not be the password itself, and must be a SHA-1 sized digest (20 bytes).
        var hashBytes = Convert.FromBase64String(dp.Hash!.Value!);
        Assert.Equal(20, hashBytes.Length);
        Assert.NotEqual("s3cret", dp.Hash.Value);
    }

    [Fact]
    public void Protection_UsesRandomSalt_AcrossExports()
    {
        var design = SingleTextDesign(new PageSettingsDto
        {
            Protection = new DocumentProtectionDto { Enabled = true, Mode = "readOnly", PasswordHash = "same" },
        });

        static string SaltOf(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var doc = WordprocessingDocument.Open(ms, false);
            return doc.MainDocumentPart!.DocumentSettingsPart!.Settings!.GetFirstChild<DocumentProtection>()!.Salt!.Value!;
        }

        var a = SaltOf(new WordDocumentExporter().Export(design));
        var b = SaltOf(new WordDocumentExporter().Export(design));

        Assert.NotEqual(a, b); // random per-export salt
    }

    [Fact]
    public void Protection_Disabled_WritesNoProtectionElement()
    {
        var design = SingleTextDesign(new PageSettingsDto
        {
            Protection = new DocumentProtectionDto { Enabled = false, PasswordHash = "x" },
        });

        var bytes = new WordDocumentExporter().Export(design);
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);

        var settingsPart = doc.MainDocumentPart!.DocumentSettingsPart;
        var dp = settingsPart?.Settings?.GetFirstChild<DocumentProtection>();
        Assert.Null(dp);
    }

    // ── SSRF-guarded remote image fetch ──────────────────────────────────────

    [Theory]
    [InlineData("http://127.0.0.1/secret.png")]
    [InlineData("http://169.254.169.254/latest/meta-data/")] // cloud metadata endpoint
    [InlineData("http://10.0.0.5/internal.png")]
    [InlineData("http://192.168.1.10/internal.png")]
    [InlineData("http://[::1]/loopback.png")]
    public void RemoteImage_PrivateOrLoopbackHost_RendersPlaceholderWithoutFetch(string url)
    {
        var design = new DesignExportDto
        {
            Id = "img",
            Name = "Img",
            Pages =
            [
                new PageDto
                {
                    Id = "p1",
                    Elements =
                    [
                        new ElementDto { Id = "i1", Type = "image", X = 0, Y = 0, Width = 100, Height = 100, Content = url },
                    ],
                },
            ],
        };

        // Must not throw and must not hang on a network call — the URL is rejected up front.
        var bytes = new WordDocumentExporter().Export(design, new ExportOptions(WordFidelityV2: false));

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        Assert.Contains("[image unavailable]", doc.MainDocumentPart!.Document!.Body!.InnerText);
    }

    // ── Digital signature C14N correctness ───────────────────────────────────

    [Fact]
    public void SignDocx_ProducesSignatureThatVerifiesAfterCanonicalization()
    {
        // Self-signed RSA cert with private key.
        using var rsaKey = RSA.Create(2048);
        var req = new CertificateRequest("CN=Canvas Test", rsaKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var pfx = cert.Export(X509ContentType.Pfx, "pw");

        var docx = new WordDocumentExporter().Export(SingleTextDesign());

        using var docxStream = new MemoryStream(docx);
        var signed = DigitalSigningService.SignDocx(docxStream, pfx, "pw");

        // Extract the signature part.
        using var zip = new ZipArchive(new MemoryStream(signed), ZipArchiveMode.Read);
        var sigEntry = zip.GetEntry("_xmlsignatures/sig1.xml");
        Assert.NotNull(sigEntry);

        string sigXml;
        using (var r = new StreamReader(sigEntry!.Open()))
            sigXml = r.ReadToEnd();

        var sigDoc = new XmlDocument { PreserveWhitespace = true };
        sigDoc.LoadXml(sigXml);
        var ns = new XmlNamespaceManager(sigDoc.NameTable);
        ns.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

        var signedInfo = (XmlElement)sigDoc.SelectSingleNode("//ds:SignedInfo", ns)!;
        var sigValue   = sigDoc.SelectSingleNode("//ds:SignatureValue", ns)!.InnerText;
        var certB64    = sigDoc.SelectSingleNode("//ds:X509Certificate", ns)!.InnerText;

        // Recompute the canonical form a verifier would and check the RSA signature over it.
        var c14n = Canonicalize(signedInfo);
        using var verifyCert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certB64));
        using var pubKey = verifyCert.GetRSAPublicKey()!;
        var verified = pubKey.VerifyData(c14n, Convert.FromBase64String(sigValue),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.True(verified, "SignedInfo signature must verify against its canonicalized form.");

        // The package <Object> reference digest must match C14N(Object).
        var objectEl   = (XmlElement)sigDoc.SelectSingleNode("//ds:Object", ns)!;
        var objDigest   = Convert.ToBase64String(SHA256.HashData(Canonicalize(objectEl)));
        var refDigest   = sigDoc.SelectSingleNode("//ds:Reference[@URI='#idPackageObject']/ds:DigestValue", ns)!.InnerText;
        Assert.Equal(objDigest, refDigest);
    }

    [Fact]
    public void SignDocx_ProducesValidOpcPackageStructure()
    {
        using var rsaKey = RSA.Create(2048);
        var req = new CertificateRequest("CN=Canvas Test", rsaKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var pfx = cert.Export(X509ContentType.Pfx, "pw");

        var docx = new WordDocumentExporter().Export(SingleTextDesign());
        using var docxStream = new MemoryStream(docx);
        var signed = DigitalSigningService.SignDocx(docxStream, pfx, "pw");

        using var zip = new ZipArchive(new MemoryStream(signed), ZipArchiveMode.Read);

        // OPC signature infrastructure parts must all be present.
        Assert.NotNull(zip.GetEntry("_xmlsignatures/sig1.xml"));
        Assert.NotNull(zip.GetEntry("_xmlsignatures/origin.sigs"));
        Assert.NotNull(zip.GetEntry("_xmlsignatures/_rels/origin.sigs.rels"));

        // Content types declare the origin + signature parts.
        var ct = ReadEntry(zip, "[Content_Types].xml");
        Assert.Contains("digital-signature-origin", ct);
        Assert.Contains("/_xmlsignatures/sig1.xml", ct);

        // Root rels point at the signature origin.
        var rootRels = ReadEntry(zip, "_rels/.rels");
        Assert.Contains("/_xmlsignatures/origin.sigs", rootRels);

        var sigDoc = new XmlDocument { PreserveWhitespace = true };
        sigDoc.LoadXml(ReadEntry(zip, "_xmlsignatures/sig1.xml"));
        var ns = new XmlNamespaceManager(sigDoc.NameTable);
        ns.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");

        // Manifest exists and references the main document part with a ContentType query.
        var manifestRefs = sigDoc.SelectNodes("//ds:Object/ds:Manifest/ds:Reference", ns)!;
        Assert.True(manifestRefs.Count > 0, "Manifest must reference signed parts.");
        var uris = manifestRefs.Cast<XmlElement>().Select(r => r.GetAttribute("URI")).ToList();
        Assert.Contains(uris, u => u.StartsWith("/word/document.xml?ContentType="));

        // The .rels part is signed through the OPC RelationshipTransform.
        var relTransform = sigDoc.SelectSingleNode(
            "//ds:Object/ds:Manifest/ds:Reference[contains(@URI,'.rels')]/ds:Transforms/ds:Transform[@Algorithm='" + RelTransformAlg + "']", ns);
        Assert.NotNull(relTransform);

        // Independently recompute one content-part digest to prove the manifest is honest.
        var docRef = manifestRefs.Cast<XmlElement>().First(r => r.GetAttribute("URI").StartsWith("/word/document.xml?"));
        var docDigest = docRef.SelectSingleNode("ds:DigestValue", ns)!.InnerText;
        var docBytes  = ReadEntryBytes(zip, "word/document.xml");
        Assert.Equal(Convert.ToBase64String(SHA256.HashData(docBytes)), docDigest);
    }

    private const string RelTransformAlg = "http://schemas.openxmlformats.org/package/2006/RelationshipTransform";

    private static string ReadEntry(ZipArchive zip, string name)
    {
        using var r = new StreamReader(zip.GetEntry(name)!.Open());
        return r.ReadToEnd();
    }

    private static byte[] ReadEntryBytes(ZipArchive zip, string name)
    {
        using var s  = zip.GetEntry(name)!.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static byte[] Canonicalize(XmlElement element)
    {
        var isolated = new XmlDocument { PreserveWhitespace = true };
        isolated.LoadXml(element.OuterXml);
        var transform = new XmlDsigC14NTransform();
        transform.LoadInput(isolated);
        using var output = (Stream)transform.GetOutput(typeof(Stream));
        using var ms = new MemoryStream();
        output.CopyTo(ms);
        return ms.ToArray();
    }
}
