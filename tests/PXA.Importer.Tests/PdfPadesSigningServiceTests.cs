using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Signing;
using PXA.Importer.Trust;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfPadesSigningServiceTests
{
    [Fact]
    public void CadesBuilder_ShouldCreateVerifiableRsaSignedAttributes()
    {
        using var fixture = SigningFixture.Create();
        var documentDigest = SHA256.HashData("PXA detached content"u8);

        var result = PdfCadesCmsBuilder.CreateDetached(documentDigest, fixture.Key, fixture.SigningTime);

        Assert.Equal(0x30, result.Cms[0]);
        Assert.True(result.SignedAttributes.AsSpan().IndexOf(documentDigest) >= 0);
        Assert.Equal(
            SHA256.HashData(fixture.Key.Certificate.RawData),
            ReadSigningCertificateV2Hash(result.SignedAttributes));
        using var publicKey = fixture.Key.Certificate.GetRSAPublicKey();
        Assert.NotNull(publicKey);
        Assert.True(publicKey.VerifyHash(
            SHA256.HashData(result.SignedAttributes),
            result.Signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public async Task Sign_ShouldCreateVerifiablePadesBaselineBRevision()
    {
        using var fixture = SigningFixture.Create();
        var source = CreatePdf();

        var result = new PdfPadesSigningService().Sign(
            source,
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "Approval",
                Reason = "Release approval",
                Location = "Zurich",
                SigningTime = fixture.SigningTime
            });

        Assert.True(result.Bytes.AsSpan(0, source.Length).SequenceEqual(source));
        Assert.Equal(PdfPadesProfile.BaselineB, result.Profile);
        Assert.Equal("SHA-256", result.DigestAlgorithm);

        await using var signedStream = new MemoryStream(result.Bytes);
        var trust = await new PdfTrustInspector().InspectAsync(signedStream);
        var signature = Assert.Single(trust.Signatures);
        Assert.Equal("ETSI.CAdES.detached", signature.SubFilter);
        Assert.Equal(4, signature.ByteRange.Count);
        Assert.Equal(0, signature.ByteRange[0]);
        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Valid, signature.CoverageStatus);
        Assert.Equal(PdfTrustStatus.Indeterminate, signature.CertificateStatus);
        Assert.Equal(PdfTrustStatus.Indeterminate, signature.Status);
        Assert.Equal("B-B", signature.PadesProfile);
        Assert.Equal("SHA-256", signature.DigestAlgorithm);
        Assert.Equal(fixture.Key.Certificate.Subject, signature.SignerSubject);
        Assert.True(signature.CoversWholeDocument);
        Assert.Equal(result.RevisionCount, trust.RevisionCount);

        var parsed = ParseSignature(result.Bytes, signature.ObjectNumber, signature.Generation);
        var documentDigest = HashByteRanges(result.Bytes, signature.ByteRange);
        var cms = parsed.Contents[..ReadDerLength(parsed.Contents)];
        var cmsData = ReadCms(cms);

        Assert.Equal(documentDigest, cmsData.MessageDigest);
        using var publicKey = fixture.Key.Certificate.GetRSAPublicKey();
        Assert.NotNull(publicKey);
        Assert.True(publicKey.VerifyHash(
            SHA256.HashData(cmsData.SignedAttributes),
            cmsData.Signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
        Assert.True(parsed.IsReferencedByAcroForm);

        var externalOutput = Environment.GetEnvironmentVariable("PXA_SIGNED_PDF_OUTPUT");
        if (!string.IsNullOrWhiteSpace(externalOutput))
        {
            await File.WriteAllBytesAsync(externalOutput, result.Bytes);
            await File.WriteAllBytesAsync($"{externalOutput}.p7s", cms);
            await File.WriteAllBytesAsync(
                $"{externalOutput}.content",
                ReadByteRanges(result.Bytes, signature.ByteRange));
        }
    }

    [Fact]
    public async Task Sign_ShouldAppendSecondSignatureWithoutChangingFirstRevisionBytes()
    {
        using var fixture = SigningFixture.Create();
        var service = new PdfPadesSigningService();
        var first = service.Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "First", SigningTime = fixture.SigningTime });

        var second = service.Sign(
            first.Bytes,
            fixture.Key,
            new PdfSigningOptions { FieldName = "Second", SigningTime = fixture.SigningTime.AddMinutes(1) });

        Assert.True(second.Bytes.AsSpan(0, first.Bytes.Length).SequenceEqual(first.Bytes));
        await using var stream = new MemoryStream(second.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        Assert.Equal(2, report.Signatures.Count);
        Assert.All(report.Signatures, signature => Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus));
        Assert.False(report.Signatures[0].CoversWholeDocument);
        Assert.True(report.Signatures[1].CoversWholeDocument);
        Assert.Equal(first.RevisionCount + 1, second.RevisionCount);
    }

    [Fact]
    public async Task Inspect_ShouldRejectCryptographicallyModifiedSignedContent()
    {
        using var fixture = SigningFixture.Create();
        var result = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime });
        var modified = result.Bytes.ToArray();
        var textOffset = modified.AsSpan().IndexOf("PXA PAdES fixture"u8);
        Assert.True(textOffset >= 0);
        modified[textOffset] ^= 1;

        await using var stream = new MemoryStream(modified);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        var signature = Assert.Single(report.Signatures);

        Assert.Equal(PdfTrustStatus.Invalid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST105");
    }

    [Fact]
    public void Sign_ShouldRejectDuplicateFieldName()
    {
        using var fixture = SigningFixture.Create();
        var service = new PdfPadesSigningService();
        var first = service.Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime });

        var exception = Assert.Throws<InvalidOperationException>(() => service.Sign(
            first.Bytes,
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime.AddMinutes(1) }));

        Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] CreatePdf()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("PXA PAdES fixture", 40, 40);
        return document.ToBytes();
    }

    private static ParsedSignature ParseSignature(byte[] bytes, int objectNumber, int generation)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var signatureId = new PdfObjectId(objectNumber, generation);
        var dictionary = Assert.IsType<PdfDictionary>(graph.Resolve(signatureId)?.Value);
        var contents = Assert.IsType<PdfString>(dictionary["Contents"]).GetDecodedBytes().ToArray();
        var catalog = Assert.IsType<PdfDictionary>(Resolve(graph, graph.Trailer?["Root"]));
        var acroForm = Assert.IsType<PdfDictionary>(Resolve(graph, catalog["AcroForm"]));
        var fields = Assert.IsType<PdfArray>(Resolve(graph, acroForm["Fields"]));
        var referencesSignature = fields.Items
            .Select(field => Resolve(graph, field))
            .OfType<PdfDictionary>()
            .Any(field => field["V"] is PdfReference reference && reference.Id == signatureId);
        return new ParsedSignature(contents, referencesSignature);
    }

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    }

    private static byte[] HashByteRanges(byte[] bytes, IReadOnlyList<long> byteRange)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(bytes.AsSpan(checked((int)byteRange[0]), checked((int)byteRange[1])));
        hash.AppendData(bytes.AsSpan(checked((int)byteRange[2]), checked((int)byteRange[3])));
        return hash.GetHashAndReset();
    }

    private static byte[] ReadByteRanges(byte[] bytes, IReadOnlyList<long> byteRange)
    {
        var first = bytes.AsSpan(checked((int)byteRange[0]), checked((int)byteRange[1]));
        var second = bytes.AsSpan(checked((int)byteRange[2]), checked((int)byteRange[3]));
        var result = new byte[first.Length + second.Length];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        return result;
    }

    private static CmsData ReadCms(ReadOnlySpan<byte> cms)
    {
        var contentInfo = new AsnReader(cms.ToArray(), AsnEncodingRules.DER).ReadSequence();
        Assert.Equal("1.2.840.113549.1.7.2", contentInfo.ReadObjectIdentifier());
        var explicitContent = contentInfo.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
        var signedData = explicitContent.ReadSequence();
        signedData.ReadInteger();
        signedData.ReadEncodedValue();
        signedData.ReadEncodedValue();
        signedData.ReadEncodedValue();
        var signerInfos = signedData.ReadSetOf();
        var signerInfo = signerInfos.ReadSequence();
        signerInfo.ReadInteger();
        signerInfo.ReadEncodedValue();
        signerInfo.ReadEncodedValue();
        var implicitAttributes = signerInfo.ReadEncodedValue().ToArray();
        implicitAttributes[0] = 0x31;
        signerInfo.ReadEncodedValue();
        var signature = signerInfo.ReadOctetString();

        byte[]? messageDigest = null;
        var attributes = new AsnReader(implicitAttributes, AsnEncodingRules.DER).ReadSetOf();
        while (attributes.HasData)
        {
            var attribute = attributes.ReadSequence();
            var oid = attribute.ReadObjectIdentifier();
            var values = attribute.ReadSetOf();
            if (oid == "1.2.840.113549.1.9.4")
            {
                messageDigest = values.ReadOctetString();
            }
        }

        return new CmsData(
            implicitAttributes,
            signature,
            Assert.IsType<byte[]>(messageDigest));
    }

    private static byte[] ReadSigningCertificateV2Hash(ReadOnlySpan<byte> signedAttributes)
    {
        var attributes = new AsnReader(signedAttributes.ToArray(), AsnEncodingRules.DER).ReadSetOf();
        while (attributes.HasData)
        {
            var attribute = attributes.ReadSequence();
            var oid = attribute.ReadObjectIdentifier();
            var values = attribute.ReadSetOf();
            if (oid != "1.2.840.113549.1.9.16.2.47")
            {
                continue;
            }

            var signingCertificate = values.ReadSequence();
            var certificateIds = signingCertificate.ReadSequence();
            var certificateId = certificateIds.ReadSequence();
            return certificateId.ReadOctetString();
        }

        throw new Xunit.Sdk.XunitException("The CAdES SigningCertificateV2 attribute is missing.");
    }

    private static int ReadDerLength(ReadOnlySpan<byte> value)
    {
        Assert.True(value.Length >= 2 && value[0] == 0x30);
        if ((value[1] & 0x80) == 0)
        {
            return 2 + value[1];
        }

        var lengthBytes = value[1] & 0x7F;
        Assert.InRange(lengthBytes, 1, 4);
        var contentLength = 0;
        for (var index = 0; index < lengthBytes; index++)
        {
            contentLength = (contentLength << 8) | value[2 + index];
        }

        return 2 + lengthBytes + contentLength;
    }

    private sealed record ParsedSignature(byte[] Contents, bool IsReferencedByAcroForm);

    private sealed record CmsData(byte[] SignedAttributes, byte[] Signature, byte[] MessageDigest);

    private sealed class SigningFixture : IDisposable
    {
        private SigningFixture(PdfPkcs12SigningKey key, DateTimeOffset signingTime)
        {
            Key = key;
            SigningTime = signingTime;
        }

        public PdfPkcs12SigningKey Key { get; }
        public DateTimeOffset SigningTime { get; }

        public static SigningFixture Create()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=PXA PDF Signing Test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
                critical: true));
            var now = DateTimeOffset.UtcNow;
            using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
            var pfx = certificate.Export(X509ContentType.Pkcs12, "test-password");
            return new SigningFixture(new PdfPkcs12SigningKey(pfx, "test-password"), now);
        }

        public void Dispose() => Key.Dispose();
    }
}
