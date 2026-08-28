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
    private const string SigningTimeAttributeOid = "1.2.840.113549.1.9.5";
    private const string SigningCertificateV2AttributeOid = "1.2.840.113549.1.9.16.2.47";
    private const string SignatureTimestampTokenAttributeOid = "1.2.840.113549.1.9.16.2.14";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string RsaEncryptionOid = "1.2.840.113549.1.1.1";

    public static PdfCadesCmsResult CreateDetached(
        ReadOnlySpan<byte> documentDigest,
        IPdfSigningKey signingKey,
        DateTimeOffset signingTime)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        if (documentDigest.Length != SHA256.HashSizeInBytes)
        {
            throw new ArgumentException("PAdES Baseline B requires a SHA-256 document digest.", nameof(documentDigest));
        }

        var certificate = signingKey.Certificate;
        var signedAttributes = BuildSignedAttributes(documentDigest, certificate, signingTime);
        var attributesDigest = SHA256.HashData(signedAttributes);
        var signature = signingKey.SignHash(attributesDigest, HashAlgorithmName.SHA256);
        var signerInfo = BuildSignerInfo(certificate, signedAttributes, signature, timestampToken: null);
        var signedData = BuildSignedData(certificate, signerInfo);
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(timestampAuthority);
        if (documentDigest.Length != SHA256.HashSizeInBytes)
        {
            throw new ArgumentException("PAdES Baseline T requires a SHA-256 document digest.", nameof(documentDigest));
        }

        var certificate = signingKey.Certificate;
        var signedAttributes = BuildSignedAttributes(documentDigest.Span, certificate, signingTime);
        var attributesDigest = SHA256.HashData(signedAttributes);
        var signature = signingKey.SignHash(attributesDigest, HashAlgorithmName.SHA256);
        var timestampToken = await timestampAuthority.GetTimestampTokenAsync(
            SHA256.HashData(signature),
            HashAlgorithmName.SHA256,
            cancellationToken).ConfigureAwait(false);
        if (timestampToken.Cms.Length == 0)
        {
            throw new CryptographicException("The timestamp authority returned an empty RFC 3161 token.");
        }

        var signerInfo = BuildSignerInfo(certificate, signedAttributes, signature, timestampToken.Cms);
        var signedData = BuildSignedData(certificate, signerInfo);
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

    private static byte[] BuildSignedAttributes(
        ReadOnlySpan<byte> documentDigest,
        X509Certificate2 certificate,
        DateTimeOffset signingTime)
    {
        var digestBytes = documentDigest.ToArray();
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSetOf();
        writer.WriteEncodedValue(BuildAttribute(ContentTypeAttributeOid, values =>
            values.WriteObjectIdentifier(DataOid)));
        writer.WriteEncodedValue(BuildAttribute(MessageDigestAttributeOid, values =>
            values.WriteOctetString(digestBytes)));
        writer.WriteEncodedValue(BuildAttribute(SigningTimeAttributeOid, values =>
            values.WriteUtcTime(signingTime)));
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
        WriteAlgorithmIdentifier(writer, Sha256Oid, includeNullParameter: false);

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

    private static byte[] BuildSignedData(X509Certificate2 certificate, byte[] signerInfo)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.WriteInteger(1);
        writer.PushSetOf();
        WriteAlgorithmIdentifier(writer, Sha256Oid, includeNullParameter: false);
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
}
