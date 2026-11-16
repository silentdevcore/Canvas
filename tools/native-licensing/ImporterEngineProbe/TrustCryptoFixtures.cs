using System.Formats.Asn1;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

internal sealed class TrustCryptoFixtures : HttpMessageHandler
{
    private readonly RSA issuerKey = RSA.Create(2048);
    private readonly RSA key = RSA.Create(2048);
    private readonly X509Certificate2 timestampCertificate;
    internal DateTimeOffset Now { get; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMinutes(-1);
    internal X509Certificate2 Issuer { get; }
    internal X509Certificate2 Certificate { get; }
    internal byte[] Pfx { get; }
    internal byte[] Crl { get; }
    internal byte[] Ocsp { get; }
    internal int Requests { get; private set; }

    internal TrustCryptoFixtures()
    {
        var ca = new CertificateRequest("CN=PVAD synthetic admission CA", issuerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        ca.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        ca.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        Issuer = ca.CreateSelfSigned(Now.AddDays(-3), Now.AddDays(3));
        var leaf = new CertificateRequest("CN=PVAD synthetic admission signer", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        leaf.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leaf.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        using (var publicLeaf = leaf.Create(Issuer, Now.AddDays(-1), Now.AddDays(1), RandomNumberGenerator.GetBytes(16)))
            Certificate = publicLeaf.CopyWithPrivateKey(key);
        Pfx = Certificate.Export(X509ContentType.Pkcs12);
        var tsa = new CertificateRequest("CN=PVAD synthetic admission TSA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        tsa.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        tsa.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.8") }, true));
        timestampCertificate = tsa.CreateSelfSigned(Now.AddDays(-1), Now.AddDays(1));
        Crl = new CertificateRevocationListBuilder().Build(Issuer, new BigInteger(1), Now.AddHours(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, Now.AddHours(-1));
        Ocsp = CreateOcsp();
    }

    private byte[] CreateOcsp()
    {
        var data = new AsnWriter(AsnEncodingRules.DER);
        data.PushSequence();
        var publicKeyReader = new AsnReader(Issuer.PublicKey.ExportSubjectPublicKeyInfo(), AsnEncodingRules.DER).ReadSequence();
        publicKeyReader.ReadEncodedValue();
        var publicKey = publicKeyReader.ReadBitString(out _);
        data.WriteOctetString(SHA1.HashData(publicKey), new Asn1Tag(TagClass.ContextSpecific, 2));
        data.WriteGeneralizedTime(Now);
        data.PushSequence(); data.PushSequence(); data.PushSequence();
        Algorithm(data, "2.16.840.1.101.3.4.2.1");
        data.WriteOctetString(SHA256.HashData(Issuer.SubjectName.RawData));
        data.WriteOctetString(SHA256.HashData(publicKey));
        var serial = Certificate.GetSerialNumber(); Array.Reverse(serial);
        var first = 0;
        while (first < serial.Length - 1 && serial[first] == 0) first++;
        data.WriteIntegerUnsigned(serial.AsSpan(first));
        data.PopSequence();
        data.WriteNull(new Asn1Tag(TagClass.ContextSpecific, 0));
        data.WriteGeneralizedTime(Now.AddMinutes(-1));
        data.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
        data.WriteGeneralizedTime(Now.AddHours(1));
        data.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
        data.PopSequence(); data.PopSequence(); data.PopSequence();
        var basic = new AsnWriter(AsnEncodingRules.DER);
        basic.PushSequence(); basic.WriteEncodedValue(data.Encode());
        Algorithm(basic, "1.2.840.113549.1.1.11");
        basic.WriteBitString(issuerKey.SignData(data.Encode(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        basic.PopSequence();
        var response = new AsnWriter(AsnEncodingRules.DER);
        response.PushSequence(); response.WriteEnumeratedValue(OcspStatus.Successful);
        response.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)); response.PushSequence();
        response.WriteObjectIdentifier("1.3.6.1.5.5.7.48.1.1"); response.WriteOctetString(basic.Encode());
        response.PopSequence(); response.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)); response.PopSequence();
        return response.Encode();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
        if (!Rfc3161TimestampRequest.TryDecode(bytes, out var decoded, out var consumed) || consumed != bytes.Length)
            throw new InvalidDataException("Invalid synthetic timestamp request.");
        var info = new Rfc3161TimestampTokenInfo(new Oid("1.2.3.4.5"), decoded!.HashAlgorithmId, decoded.GetMessageHash(), new byte[] { 1 }, Now,
            accuracyInMicroseconds: null, isOrdering: false, nonce: decoded.GetNonce(), timestampAuthorityName: null, extensions: null);
        var cms = new SignedCms(new ContentInfo(new Oid("1.2.840.113549.1.9.16.1.4"), info.Encode()), false);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, timestampCertificate)
        {
            IncludeOption = X509IncludeOption.EndCertOnly, DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1")
        };
        var certificateHash = new AsnWriter(AsnEncodingRules.DER);
        certificateHash.PushSequence(); certificateHash.PushSequence(); certificateHash.PushSequence();
        certificateHash.WriteOctetString(SHA256.HashData(timestampCertificate.RawData));
        certificateHash.PopSequence(); certificateHash.PopSequence(); certificateHash.PopSequence();
        signer.SignedAttributes.Add(new AsnEncodedData(new Oid("1.2.840.113549.1.9.16.2.47"), certificateHash.Encode()));
        cms.ComputeSignature(signer);
        var response = new AsnWriter(AsnEncodingRules.DER);
        response.PushSequence(); response.PushSequence(); response.WriteInteger(0); response.PopSequence();
        response.WriteEncodedValue(cms.Encode()); response.PopSequence();
        var result = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(response.Encode()) };
        result.Content.Headers.ContentType = new("application/timestamp-reply");
        return result;
    }

    private static void Algorithm(AsnWriter writer, string oid)
    {
        writer.PushSequence(); writer.WriteObjectIdentifier(oid); writer.WriteNull(); writer.PopSequence();
    }
    private enum OcspStatus { Successful }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CryptographicOperations.ZeroMemory(Pfx);
            Certificate.Dispose(); Issuer.Dispose(); timestampCertificate.Dispose(); key.Dispose(); issuerKey.Dispose();
        }
        base.Dispose(disposing);
    }
}
