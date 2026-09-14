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
var generatedPdfs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
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
    generatedPdfs[id] = result.Bytes;

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
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "root-signer-revoked.crl"),
    pki.SignerRevokedCertificateRevocationList);

await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "pxa-pades-revoked.pdf"),
    generatedPdfs["pxa-pades-b-t"]);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "pxa-pades-untrusted.pdf"),
    generatedPdfs["pxa-pades-b-t"]);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "pxa-pades-revocation-unavailable.pdf"),
    generatedPdfs["pxa-pades-b-t"]);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "pxa-pades-tampered-lt.pdf"),
    TamperEmbeddedBytes(generatedPdfs["pxa-pades-b-lt"], pki.CertificateRevocationList));
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "pxa-pades-tampered-lta.pdf"),
    TamperLastDocumentTimestamp(generatedPdfs["pxa-pades-b-lta"]));

using var untrustedPki = SyntheticPki.Create(signingTime, "Untrusted");
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "untrusted-root.cer"),
    untrustedPki.RootCertificate.RawData);

using var expiredPki = SyntheticPki.Create(
    signingTime,
    "Expired",
    signerNotAfter: signingTime.AddDays(1));
var expiredResult = await service.SignAsync(
    CreatePdf("pxa-pades-expired"),
    expiredPki.Signer.Key,
    new PdfSigningOptions
    {
        FieldName = "SyntheticExpiredApproval",
        Reason = "PXA expired-certificate validation fixture",
        Location = "Zurich",
        SigningTime = signingTime,
        Profile = PdfPadesProfile.BaselineB
    },
    timestampAuthority: null);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "pxa-pades-expired.pdf"),
    expiredResult.Bytes);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "expired-root.cer"),
    expiredPki.RootCertificate.RawData);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "expired-root-current.crl"),
    expiredPki.CertificateRevocationList);
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
        }).ToArray()),
        ["negativeFixtures"] = new JsonArray(
            new JsonObject { ["id"] = "pxa-pades-revoked", ["expectedSubIndication"] = "REVOKED_NO_POE" },
            new JsonObject { ["id"] = "pxa-pades-untrusted", ["expectedSubIndication"] = "NO_CERTIFICATE_CHAIN_FOUND" },
            new JsonObject { ["id"] = "pxa-pades-revocation-unavailable", ["expectedSubIndication"] = "CERTIFICATE_CHAIN_GENERAL_FAILURE" },
            new JsonObject { ["id"] = "pxa-pades-expired", ["expectedSubIndication"] = "OUT_OF_BOUNDS_NOT_REVOKED" },
            new JsonObject { ["id"] = "pxa-pades-tampered-lt", ["expectedSubIndication"] = "CERTIFICATE_CHAIN_GENERAL_FAILURE" },
            new JsonObject { ["id"] = "pxa-pades-tampered-lta", ["expectedSubIndication"] = "SIG_CRYPTO_FAILURE" })
    }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
    new UTF8Encoding(false));

Console.WriteLine($"Wrote {fixtures.Count} positive and 6 negative PXA PAdES fixtures to {outputDirectory}.");

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

static byte[] TamperEmbeddedBytes(byte[] pdf, byte[] embeddedValue)
{
    var offset = pdf.AsSpan().IndexOf(embeddedValue);
    if (offset < 0)
        throw new InvalidDataException("The embedded validation material was not found in the generated PDF.");

    var tampered = pdf.ToArray();
    tampered[offset + embeddedValue.Length / 2] ^= 0x01;
    return tampered;
}

static byte[] TamperLastDocumentTimestamp(byte[] pdf)
{
    var marker = "/SubFilter /ETSI.RFC3161"u8;
    var markerOffset = pdf.AsSpan().LastIndexOf(marker);
    if (markerOffset < 0)
        throw new InvalidDataException("The generated B-LTA PDF has no document timestamp signature.");

    var contentsMarker = "/Contents <"u8;
    var contentsOffset = pdf.AsSpan(0, markerOffset).LastIndexOf(contentsMarker);
    if (contentsOffset < 0)
        throw new InvalidDataException("The generated document timestamp has no hexadecimal Contents value.");

    var tampered = pdf.ToArray();
    var contentsStart = contentsOffset + contentsMarker.Length;
    var contentsEnd = pdf.AsSpan(contentsStart).IndexOf((byte)'>');
    if (contentsEnd < 0)
        throw new InvalidDataException("The generated document timestamp Contents value is not terminated.");

    var encodedCms = Convert.FromHexString(Encoding.ASCII.GetString(
        pdf.AsSpan(contentsStart, contentsEnd)));
    var cmsLength = ReadDerValueLength(encodedCms);
    var cms = new SignedCms();
    cms.Decode(encodedCms.AsSpan(0, cmsLength).ToArray());
    var signature = cms.SignerInfos[0].GetSignature();
    var signatureOffset = encodedCms.AsSpan(0, cmsLength).IndexOf(signature);
    if (signatureOffset < 0)
        throw new InvalidDataException("The generated document timestamp signature value was not found.");

    var hexOffset = contentsStart + (signatureOffset + signature.Length / 2) * 2;
    tampered[hexOffset] = tampered[hexOffset] == (byte)'0' ? (byte)'1' : (byte)'0';
    return tampered;
}

static int ReadDerValueLength(ReadOnlySpan<byte> encoded)
{
    if (encoded.Length < 2)
        throw new InvalidDataException("The timestamp CMS is truncated.");

    var lengthByte = encoded[1];
    if ((lengthByte & 0x80) == 0)
        return checked(2 + lengthByte);

    var lengthBytes = lengthByte & 0x7f;
    if (lengthBytes is 0 or > 4 || encoded.Length < 2 + lengthBytes)
        throw new InvalidDataException("The timestamp CMS has an invalid DER length.");

    var contentLength = 0;
    for (var index = 0; index < lengthBytes; index++)
        contentLength = checked((contentLength << 8) | encoded[2 + index]);
    var totalLength = checked(2 + lengthBytes + contentLength);
    if (totalLength > encoded.Length)
        throw new InvalidDataException("The timestamp CMS DER value exceeds its Contents payload.");
    return totalLength;
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
        byte[] certificateRevocationList,
        byte[] signerRevokedCertificateRevocationList)
    {
        RootCertificate = rootCertificate;
        Signer = signer;
        TimestampAuthority = timestampAuthority;
        CertificateRevocationList = certificateRevocationList;
        SignerRevokedCertificateRevocationList = signerRevokedCertificateRevocationList;
    }

    public X509Certificate2 RootCertificate { get; }
    public SyntheticSigner Signer { get; }
    public SyntheticTimestampAuthority TimestampAuthority { get; }
    public byte[] CertificateRevocationList { get; }
    public byte[] SignerRevokedCertificateRevocationList { get; }

    public static SyntheticPki Create(
        DateTimeOffset signingTime,
        string nameSuffix = "Primary",
        DateTimeOffset? signerNotAfter = null)
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            $"CN=PXA Synthetic PAdES {nameSuffix} Root CA,O=PXA Synthetic Tests,C=CH",
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
            var signer = SyntheticSigner.Create(root, signingTime, signerNotAfter);
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
                var revokedBuilder = new CertificateRevocationListBuilder();
                revokedBuilder.AddEntry(signer.Key.Certificate, signingTime.AddMinutes(-10));
                var revokedCrl = revokedBuilder.Build(
                    root,
                    new BigInteger(2),
                    signingTime.AddYears(4),
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1,
                    signingTime.AddHours(-1));
                return new SyntheticPki(root, signer, timestampAuthority, crl, revokedCrl);
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

    public static SyntheticSigner Create(
        X509Certificate2 issuer,
        DateTimeOffset signingTime,
        DateTimeOffset? notAfter = null)
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
            notAfter ?? signingTime.AddYears(4),
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
