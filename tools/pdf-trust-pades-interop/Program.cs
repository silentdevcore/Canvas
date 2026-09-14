using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PXA.Importer.Signing;
using PXA.Importer.Trust;
using PXA.Pdf;

var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
var outputDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(repositoryRoot, "artifacts", "pdf-trust-pades-interop");
Directory.CreateDirectory(outputDirectory);

var signingTime = new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
using var pki = SyntheticPki.Create(signingTime);
var validationMaterial = new PdfLongTermValidationMaterial
{
    Certificates = [pki.Signer.Key.Certificate.RawData, pki.TimestampAuthority.Certificate.RawData],
    Crls = [pki.CertificateRevocationList],
    TrustAnchors = [pki.RootCertificate.RawData]
};

var service = new PdfPadesSigningService();
var fixtures = new List<PadesFixture>();
foreach (var profile in new[]
         {
             PdfPadesProfile.BaselineT,
             PdfPadesProfile.BaselineLT,
             PdfPadesProfile.BaselineLTA
         })
{
    var id = profile switch
    {
        PdfPadesProfile.BaselineT => "pxa-pades-b-t",
        PdfPadesProfile.BaselineLT => "pxa-pades-b-lt",
        PdfPadesProfile.BaselineLTA => "pxa-pades-b-lta",
        _ => throw new InvalidOperationException($"Unsupported profile {profile}.")
    };
    var result = await service.SignAsync(
        CreatePdf(id),
        pki.Signer.Key,
        new PdfSigningOptions
        {
            FieldName = "SyntheticApproval",
            Reason = "PXA PAdES interoperability verification",
            Location = "Zurich",
            SigningTime = signingTime,
            Profile = profile,
            LongTermValidation = profile is PdfPadesProfile.BaselineLT or PdfPadesProfile.BaselineLTA
                ? validationMaterial
                : null
        },
        pki.TimestampAuthority);
    var pdfPath = Path.Combine(outputDirectory, $"{id}.pdf");
    await File.WriteAllBytesAsync(pdfPath, result.Bytes);

    await using var stream = new MemoryStream(result.Bytes, writable: false);
    var report = await new PdfTrustInspector().InspectAsync(
        stream,
        new PdfTrustInspectionOptions
        {
            SignatureValidationPolicy = new PdfSignatureValidationPolicy
            {
                UseSystemTrustStore = false,
                CustomTrustAnchors = [pki.RootCertificate],
                IntermediateCertificates = [pki.Signer.Key.Certificate, pki.TimestampAuthority.Certificate]
            }
        });
    var approval = report.Signatures.Single(static signature => !signature.IsDocumentTimestamp);
    fixtures.Add(new PadesFixture(
        id,
        profile.ToString(),
        approval.PadesProfile,
        Path.GetFileName(pdfPath),
        Convert.ToHexString(SHA256.HashData(result.Bytes)).ToLowerInvariant(),
        result.Bytes.LongLength));
}

await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "root-trust-anchor.cer"),
    pki.RootCertificate.RawData);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "root-current.crl"),
    pki.CertificateRevocationList);
await File.WriteAllTextAsync(
    Path.Combine(outputDirectory, "pxa-pades-corpus.json"),
    new JsonObject
    {
        ["schemaVersion"] = 1,
        ["generator"] = "PXA PdfPadesSigningService",
        ["signingTimeUtc"] = signingTime.ToString("O"),
        ["fixtures"] = new JsonArray(fixtures.Select(fixture => (JsonNode)new JsonObject
        {
            ["id"] = fixture.Id,
            ["requestedProfile"] = fixture.RequestedProfile,
            ["pxaClassifiedProfile"] = fixture.PxaClassifiedProfile,
            ["pdf"] = fixture.Pdf,
            ["sha256"] = fixture.Sha256,
            ["fileSizeBytes"] = fixture.FileSizeBytes
        }).ToArray())
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
    new UTF8Encoding(false));

Console.WriteLine($"Wrote {fixtures.Count} PXA PAdES fixtures to {outputDirectory}.");

static byte[] CreatePdf(string id)
{
#pragma warning disable PXA0001
    var document = new PdfDocument();
#pragma warning restore PXA0001
    document.Info.Title = $"PXA PAdES interoperability fixture {id}";
    document.Info.Author = "PXA synthetic interoperability corpus";
    document.Info.CreationDate = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
    document.Info.ModificationDate = document.Info.CreationDate;
    document.AddPage().DrawText($"Synthetic {id} document", 40, 40);
    return document.ToBytes();
}

static string FindRepositoryRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PXA.sln")))
        directory = directory.Parent;
    return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the PXA repository root.");
}

internal sealed record PadesFixture(
    string Id,
    string RequestedProfile,
    string? PxaClassifiedProfile,
    string Pdf,
    string Sha256,
    long FileSizeBytes);

internal sealed class SyntheticPki : IDisposable
{
    private SyntheticPki(
        X509Certificate2 rootCertificate,
        SyntheticSigner signer,
        SyntheticTimestampAuthority timestampAuthority,
        byte[] certificateRevocationList)
    {
        RootCertificate = rootCertificate;
        Signer = signer;
        TimestampAuthority = timestampAuthority;
        CertificateRevocationList = certificateRevocationList;
    }

    public X509Certificate2 RootCertificate { get; }
    public SyntheticSigner Signer { get; }
    public SyntheticTimestampAuthority TimestampAuthority { get; }
    public byte[] CertificateRevocationList { get; }

    public static SyntheticPki Create(DateTimeOffset signingTime)
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=PXA Synthetic PAdES Root CA,O=PXA Synthetic Tests,C=CH",
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            critical: true));
        rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));
        var root = rootRequest.CreateSelfSigned(signingTime.AddDays(-2), signingTime.AddYears(5));
        try
        {
            var signer = SyntheticSigner.Create(root, signingTime);
            try
            {
                var timestampAuthority = SyntheticTimestampAuthority.Create(root, signingTime.AddSeconds(5));
                var crl = new CertificateRevocationListBuilder().Build(
                    root,
                    new BigInteger(1),
                    signingTime.AddYears(4),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1,
                    signingTime.AddHours(-1));
                return new SyntheticPki(root, signer, timestampAuthority, crl);
            }
            catch
            {
                signer.Dispose();
                throw;
            }
        }
        catch
        {
            root.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        TimestampAuthority.Dispose();
        Signer.Dispose();
        RootCertificate.Dispose();
    }
}

internal sealed class SyntheticSigner : IDisposable
{
    private SyntheticSigner(PdfPkcs12SigningKey key) => Key = key;

    public PdfPkcs12SigningKey Key { get; }

    public static SyntheticSigner Create(X509Certificate2 issuer, DateTimeOffset signingTime)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PXA Synthetic PAdES Signer,O=PXA Synthetic Tests,C=CH",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
            critical: true));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var issuedCertificate = request.Create(
            issuer,
            signingTime.AddDays(-1),
            signingTime.AddYears(4),
            RandomNumberGenerator.GetBytes(16));
        using var certificate = issuedCertificate.CopyWithPrivateKey(rsa);
        var pfx = certificate.Export(X509ContentType.Pkcs12, "synthetic-build-only");
        try
        {
            return new SyntheticSigner(new PdfPkcs12SigningKey(pfx, "synthetic-build-only"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    public void Dispose() => Key.Dispose();
}

internal sealed class SyntheticTimestampAuthority : IPdfTimestampAuthority, IDisposable
{
    private const string TimestampInfoOid = "1.2.840.113549.1.9.16.1.4";
    private const string TimestampingExtendedKeyUsageOid = "1.3.6.1.5.5.7.3.8";
    private readonly X509Certificate2 _certificate;

    private SyntheticTimestampAuthority(X509Certificate2 certificate, DateTimeOffset timestamp)
    {
        _certificate = certificate;
        Timestamp = timestamp;
    }

    public DateTimeOffset Timestamp { get; }
    public X509Certificate2 Certificate => _certificate;

    public static SyntheticTimestampAuthority Create(X509Certificate2 issuer, DateTimeOffset timestamp)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PXA Synthetic TSA,O=PXA Synthetic Tests,C=CH",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature,
            critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new(TimestampingExtendedKeyUsageOid) },
            critical: true));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var issuedCertificate = request.Create(
            issuer,
            timestamp.AddDays(-1),
            timestamp.AddYears(4),
            RandomNumberGenerator.GetBytes(16));
        return new SyntheticTimestampAuthority(issuedCertificate.CopyWithPrivateKey(rsa), timestamp);
    }

    public Task<PdfTimestampToken> GetTimestampTokenAsync(
        ReadOnlyMemory<byte> signatureDigest,
        HashAlgorithmName digestAlgorithm,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (digestAlgorithm != HashAlgorithmName.SHA256)
            throw new NotSupportedException("The synthetic TSA supports SHA-256 only.");

        var tokenInfo = new Rfc3161TimestampTokenInfo(
            new Oid("1.2.3.4.5"),
            new Oid("2.16.840.1.101.3.4.2.1"),
            signatureDigest,
            new byte[] { 1 },
            Timestamp,
            accuracyInMicroseconds: null,
            isOrdering: false,
            nonce: null,
            timestampAuthorityName: null,
            extensions: null);
        var cms = new SignedCms(
            new ContentInfo(new Oid(TimestampInfoOid), tokenInfo.Encode()),
            detached: false);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _certificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly,
            DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1")
        };
        signer.SignedAttributes.Add(new AsnEncodedData(
            new Oid("1.2.840.113549.1.9.16.2.47"),
            BuildSigningCertificateV2(_certificate)));
        cms.ComputeSignature(signer);
        return Task.FromResult(new PdfTimestampToken(
            cms.Encode(),
            Timestamp,
            _certificate.Subject,
            _certificate.Thumbprint));
    }

    public void Dispose() => _certificate.Dispose();

    private static byte[] BuildSigningCertificateV2(X509Certificate2 certificate)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.PushSequence();
        writer.PushSequence();
        writer.WriteOctetString(SHA256.HashData(certificate.RawData));
        writer.PopSequence();
        writer.PopSequence();
        writer.PopSequence();
        return writer.Encode();
    }
}
