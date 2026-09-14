using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PXA.Importer.Signing;

internal sealed record PdfCadesCmsResult(
    byte[] Cms,
    byte[] SignedAttributes,
    byte[] Signature,
    byte[] DocumentDigest,
    PdfTimestampToken? TimestampToken = null);

internal static class PdfCadesCmsBuilder
{
    private const string DataOid = "1.2.840.113549.1.7.1";
    private const string SignedDataOid = "1.2.840.113549.1.7.2";
    private const string ContentTypeAttributeOid = "1.2.840.113549.1.9.3";
    private const string MessageDigestAttributeOid = "1.2.840.113549.1.9.4";
    private const string SigningCertificateV2AttributeOid = "1.2.840.113549.1.9.16.2.47";
    private const string SignatureTimestampTokenAttributeOid = "1.2.840.113549.1.9.16.2.14";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string Sha384Oid = "2.16.840.1.101.3.4.2.2";
    private const string Sha512Oid = "2.16.840.1.101.3.4.2.3";
    private const string RsaEncryptionOid = "1.2.840.113549.1.1.1";

    public static PdfCadesCmsResult CreateDetached(
        ReadOnlySpan<byte> documentDigest,
        IPdfSigningKey signingKey,
        DateTimeOffset signingTime) =>
        CreateDetached(documentDigest, signingKey, signingTime, HashAlgorithmName.SHA256);

    public static PdfCadesCmsResult CreateDetached(
        ReadOnlySpan<byte> documentDigest,
        IPdfSigningKey signingKey,
        DateTimeOffset signingTime,
        HashAlgorithmName digestAlgorithm)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        if (documentDigest.Length != GetDigestSize(digestAlgorithm))
        {
            throw new ArgumentException("The document digest length does not match the requested algorithm.", nameof(documentDigest));
        }

        var certificate = signingKey.Certificate;
        var signedAttributes = BuildSignedAttributes(documentDigest, certificate);
        var attributesDigest = HashData(digestAlgorithm, signedAttributes);
        var signature = signingKey.SignHash(attributesDigest, digestAlgorithm);
        var signerInfo = BuildSignerInfo(certificate, signedAttributes, signature, digestAlgorithm, timestampToken: null);
        var signedData = BuildSignedData(certificate, signerInfo, digestAlgorithm);
        var contentInfo = new AsnWriter(AsnEncodingRules.DER);
        contentInfo.PushSequence();
        contentInfo.WriteObjectIdentifier(SignedDataOid);
        contentInfo.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
        contentInfo.WriteEncodedValue(signedData);
        contentInfo.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
        contentInfo.PopSequence();

        return new PdfCadesCmsResult(
            contentInfo.Encode(),
            signedAttributes,
            signature,
            documentDigest.ToArray());
    }

    public static async Task<PdfCadesCmsResult> CreateDetachedWithTimestampAsync(
        ReadOnlyMemory<byte> documentDigest,
        IPdfSigningKey signingKey,
        DateTimeOffset signingTime,
        IPdfTimestampAuthority timestampAuthority,
        HashAlgorithmName digestAlgorithm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(timestampAuthority);
        if (documentDigest.Length != GetDigestSize(digestAlgorithm))
        {
            throw new ArgumentException("The document digest length does not match the requested algorithm.", nameof(documentDigest));
        }

        var certificate = signingKey.Certificate;
        var signedAttributes = BuildSignedAttributes(documentDigest.Span, certificate);
        var attributesDigest = HashData(digestAlgorithm, signedAttributes);
        var signature = signingKey.SignHash(attributesDigest, digestAlgorithm);
        var timestampToken = await timestampAuthority.GetTimestampTokenAsync(
            HashData(digestAlgorithm, signature),
            digestAlgorithm,
            cancellationToken).ConfigureAwait(false);
        if (timestampToken.Cms.Length == 0)
        {
            throw new CryptographicException("The timestamp authority returned an empty RFC 3161 token.");
        }

        var signerInfo = BuildSignerInfo(certificate, signedAttributes, signature, digestAlgorithm, timestampToken.Cms);
        var signedData = BuildSignedData(certificate, signerInfo, digestAlgorithm);
        var contentInfo = new AsnWriter(AsnEncodingRules.DER);
        contentInfo.PushSequence();
        contentInfo.WriteObjectIdentifier(SignedDataOid);
        contentInfo.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
        contentInfo.WriteEncodedValue(signedData);
        contentInfo.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
        contentInfo.PopSequence();

        return new PdfCadesCmsResult(
            contentInfo.Encode(),
            signedAttributes,
            signature,
            documentDigest.ToArray(),
            timestampToken);
    }

    public static Task<PdfCadesCmsResult> CreateDetachedWithTimestampAsync(
        ReadOnlyMemory<byte> documentDigest,
        IPdfSigningKey signingKey,
        DateTimeOffset signingTime,
        IPdfTimestampAuthority timestampAuthority,
        CancellationToken cancellationToken = default) =>
        CreateDetachedWithTimestampAsync(
            documentDigest,
            signingKey,
            signingTime,
            timestampAuthority,
            HashAlgorithmName.SHA256,
            cancellationToken);

    private static byte[] BuildSignedAttributes(
        ReadOnlySpan<byte> documentDigest,
        X509Certificate2 certificate)
    {
        var digestBytes = documentDigest.ToArray();
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSetOf();
        writer.WriteEncodedValue(BuildAttribute(ContentTypeAttributeOid, values =>
            values.WriteObjectIdentifier(DataOid)));
        writer.WriteEncodedValue(BuildAttribute(MessageDigestAttributeOid, values =>
            values.WriteOctetString(digestBytes)));
        writer.WriteEncodedValue(BuildAttribute(SigningCertificateV2AttributeOid, values =>
        {
            values.PushSequence();
            values.PushSequence();
            values.PushSequence();
            values.WriteOctetString(SHA256.HashData(certificate.RawData));
            values.PopSequence();
            values.PopSequence();
            values.PopSequence();
        }));
        writer.PopSetOf();
        return writer.Encode();
    }

    private static byte[] BuildAttribute(string oid, Action<AsnWriter> writeValue)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.WriteObjectIdentifier(oid);
        writer.PushSetOf();
        writeValue(writer);
        writer.PopSetOf();
        writer.PopSequence();
        return writer.Encode();
    }

    private static byte[] BuildSignerInfo(
        X509Certificate2 certificate,
        byte[] signedAttributes,
        byte[] signature,
        HashAlgorithmName digestAlgorithm,
        byte[]? timestampToken)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.WriteInteger(1);
        writer.PushSequence();
        writer.WriteEncodedValue(certificate.IssuerName.RawData);
        var serialNumber = certificate.GetSerialNumber();
        Array.Reverse(serialNumber);
        writer.WriteIntegerUnsigned(serialNumber);
        writer.PopSequence();
        WriteAlgorithmIdentifier(writer, GetDigestOid(digestAlgorithm), includeNullParameter: false);

        var implicitAttributes = signedAttributes.ToArray();
        implicitAttributes[0] = 0xA0;
        writer.WriteEncodedValue(implicitAttributes);

        WriteAlgorithmIdentifier(writer, RsaEncryptionOid);
        writer.WriteOctetString(signature);
        if (timestampToken is not null)
        {
            var unsignedAttributes = new AsnWriter(AsnEncodingRules.DER);
            unsignedAttributes.PushSetOf();
            unsignedAttributes.WriteEncodedValue(BuildAttribute(
                SignatureTimestampTokenAttributeOid,
                values => values.WriteEncodedValue(timestampToken)));
            unsignedAttributes.PopSetOf();
            var implicitUnsignedAttributes = unsignedAttributes.Encode();
            implicitUnsignedAttributes[0] = 0xA1;
            writer.WriteEncodedValue(implicitUnsignedAttributes);
        }

        writer.PopSequence();
        return writer.Encode();
    }

    private static byte[] BuildSignedData(
        X509Certificate2 certificate,
        byte[] signerInfo,
        HashAlgorithmName digestAlgorithm)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.WriteInteger(1);
        writer.PushSetOf();
        WriteAlgorithmIdentifier(writer, GetDigestOid(digestAlgorithm), includeNullParameter: false);
        writer.PopSetOf();
        writer.PushSequence();
        writer.WriteObjectIdentifier(DataOid);
        writer.PopSequence();

        var certificateSetTag = new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true);
        writer.PushSetOf(certificateSetTag);
        writer.WriteEncodedValue(certificate.RawData);
        writer.PopSetOf(certificateSetTag);

        writer.PushSetOf();
        writer.WriteEncodedValue(signerInfo);
        writer.PopSetOf();
        writer.PopSequence();
        return writer.Encode();
    }

    private static void WriteAlgorithmIdentifier(
        AsnWriter writer,
        string oid,
        bool includeNullParameter = true)
    {
        writer.PushSequence();
        writer.WriteObjectIdentifier(oid);
        if (includeNullParameter)
        {
            writer.WriteNull();
        }

        writer.PopSequence();
    }

    private static int GetDigestSize(HashAlgorithmName algorithm) => algorithm.Name switch
    {
        "SHA256" => SHA256.HashSizeInBytes,
        "SHA384" => SHA384.HashSizeInBytes,
        "SHA512" => SHA512.HashSizeInBytes,
        _ => throw new NotSupportedException($"Digest algorithm '{algorithm.Name}' is not supported.")
    };

    private static string GetDigestOid(HashAlgorithmName algorithm) => algorithm.Name switch
    {
        "SHA256" => Sha256Oid,
        "SHA384" => Sha384Oid,
        "SHA512" => Sha512Oid,
        _ => throw new NotSupportedException($"Digest algorithm '{algorithm.Name}' is not supported.")
    };

    private static byte[] HashData(HashAlgorithmName algorithm, ReadOnlySpan<byte> value) => algorithm.Name switch
    {
        "SHA256" => SHA256.HashData(value),
        "SHA384" => SHA384.HashData(value),
        "SHA512" => SHA512.HashData(value),
        _ => throw new NotSupportedException($"Digest algorithm '{algorithm.Name}' is not supported.")
    };
}
