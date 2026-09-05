using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PXA.Importer.Trust;

public enum PdfRevocationEvidenceKind
{
    Crl,
    Ocsp
}

public sealed record PdfRevocationEvidence(
    PdfRevocationEvidenceKind Kind,
    PdfTrustStatus Status,
    string Source,
    DateTimeOffset ThisUpdate,
    DateTimeOffset? NextUpdate,
    byte[] EncodedValue);

public sealed record PdfRevocationEvidenceResult(
    PdfTrustStatus Status,
    IReadOnlyList<PdfRevocationEvidence> Evidence,
    string? Detail = null)
{
    public static PdfRevocationEvidenceResult Unavailable(string? detail = null) =>
        new(PdfTrustStatus.Indeterminate, [], detail);
}

public sealed record PdfRevocationEvidenceRequest(
    X509Certificate2 Certificate,
    X509Certificate2 Issuer,
    DateTimeOffset ValidationTime,
    PdfCertificateRevocationMode Mode);

public interface IPdfRevocationEvidenceProvider
{
    Task<PdfRevocationEvidenceResult> GetEvidenceAsync(
        PdfRevocationEvidenceRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PdfIssuerCertificateRequest(
    X509Certificate2 Certificate,
    DateTimeOffset ValidationTime,
    PdfCertificateRevocationMode Mode);

public sealed record PdfIssuerCertificateResult(
    IReadOnlyList<byte[]> Certificates,
    string? Detail = null);

public interface IPdfIssuerCertificateProvider
{
    Task<PdfIssuerCertificateResult> GetIssuerCertificatesAsync(
        PdfIssuerCertificateRequest request,
        CancellationToken cancellationToken = default);
}

public static class PdfCertificateEvidenceLocator
{
    private const string CrlDistributionPointsOid = "2.5.29.31";

    public static IReadOnlyList<Uri> GetCrlDistributionPointUris(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions[CrlDistributionPointsOid];
        if (extension is null)
        {
            return [];
        }

        var values = new List<Uri>();
        CollectUris(extension.RawData, values, 0);
        return values.DistinctBy(static uri => uri.AbsoluteUri, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<Uri> GetOcspUris(X509Certificate2 certificate)
    {
        const string authorityInformationAccessOid = "1.3.6.1.5.5.7.1.1";
        var rawExtension = certificate.Extensions[authorityInformationAccessOid];
        if (rawExtension is null)
        {
            return [];
        }

        var extension = new X509AuthorityInformationAccessExtension(
            rawExtension.RawData,
            rawExtension.Critical);
        return extension.EnumerateOcspUris()
            .Select(static value => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null)
            .Where(static uri => uri is not null)
            .Cast<Uri>()
            .ToArray();
    }

    public static IReadOnlyList<Uri> GetIssuerCertificateUris(X509Certificate2 certificate)
    {
        const string authorityInformationAccessOid = "1.3.6.1.5.5.7.1.1";
        var rawExtension = certificate.Extensions[authorityInformationAccessOid];
        if (rawExtension is null)
        {
            return [];
        }

        var extension = new X509AuthorityInformationAccessExtension(
            rawExtension.RawData,
            rawExtension.Critical);
        return extension.EnumerateCAIssuersUris()
            .Select(static value => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null)
            .Where(static uri => uri is not null)
            .Cast<Uri>()
            .ToArray();
    }

    private static void CollectUris(ReadOnlyMemory<byte> encoded, ICollection<Uri> values, int depth)
    {
        if (depth > 12)
        {
            throw new CryptographicException("The CRL distribution point extension is too deeply nested.");
        }

        var reader = new AsnReader(encoded, AsnEncodingRules.DER);
        while (reader.HasData)
        {
            var tag = reader.PeekTag();
            if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 6)
            {
                var value = reader.ReadCharacterString(
                    UniversalTagNumber.IA5String,
                    new Asn1Tag(TagClass.ContextSpecific, 6));
                if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
                {
                    values.Add(uri);
                }
                continue;
            }

            var nested = reader.ReadEncodedValue();
            if (tag.IsConstructed)
            {
                var content = ReadContent(nested);
                CollectUris(content, values, depth + 1);
            }
        }
    }

    private static ReadOnlyMemory<byte> ReadContent(ReadOnlyMemory<byte> encoded)
    {
        var span = encoded.Span;
        var offset = 1;
        if ((span[offset] & 0x80) == 0)
        {
            return encoded.Slice(2, span[offset]);
        }

        var lengthBytes = span[offset] & 0x7F;
        if (lengthBytes is < 1 or > 4 || span.Length < 2 + lengthBytes)
        {
            throw new CryptographicException("The extension contains an invalid DER length.");
        }

        var length = 0;
        for (var index = 0; index < lengthBytes; index++)
        {
            length = checked((length << 8) | span[2 + index]);
        }
        offset = 2 + lengthBytes;
        if (offset + length != span.Length)
        {
            throw new CryptographicException("The extension contains an inconsistent DER length.");
        }
        return encoded.Slice(offset, length);
    }
}

public static class PdfCrlEvidenceValidator
{
    private const string CrlNumberOid = "2.5.29.20";
    private const string CrlReasonOid = "2.5.29.21";
    private const string InvalidityDateOid = "2.5.29.24";
    private const string DeltaCrlIndicatorOid = "2.5.29.27";
    private const string IssuingDistributionPointOid = "2.5.29.28";
    private const string CertificateIssuerOid = "2.5.29.29";
    private const string AuthorityKeyIdentifierOid = "2.5.29.35";
    private const string Sha256WithRsaOid = "1.2.840.113549.1.1.11";
    private const string Sha384WithRsaOid = "1.2.840.113549.1.1.12";
    private const string Sha512WithRsaOid = "1.2.840.113549.1.1.13";
    private const string EcdsaWithSha256Oid = "1.2.840.10045.4.3.2";
    private const string EcdsaWithSha384Oid = "1.2.840.10045.4.3.3";
    private const string EcdsaWithSha512Oid = "1.2.840.10045.4.3.4";

    public static PdfRevocationEvidence Validate(
        ReadOnlyMemory<byte> encodedCrl,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        DateTimeOffset validationTime,
        string source) => ValidateCore(
            encodedCrl,
            certificate,
            issuer,
            validationTime,
            source,
            historical: false);

    public static PdfRevocationEvidence ValidateHistorical(
        ReadOnlyMemory<byte> encodedCrl,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        DateTimeOffset trustedReferenceTime,
        string source) => ValidateCore(
            encodedCrl,
            certificate,
            issuer,
            trustedReferenceTime,
            source,
            historical: true);

    private static PdfRevocationEvidence ValidateCore(
        ReadOnlyMemory<byte> encodedCrl,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        DateTimeOffset validationTime,
        string source,
        bool historical)
    {
        var reader = new AsnReader(encodedCrl, AsnEncodingRules.DER);
        var certificateList = reader.ReadSequence();
        var encodedTbs = certificateList.ReadEncodedValue();
        var signatureAlgorithm = ReadAlgorithmIdentifier(certificateList.ReadSequence());
        var signature = certificateList.ReadBitString(out var unusedBitCount);
        certificateList.ThrowIfNotEmpty();
        reader.ThrowIfNotEmpty();
        if (unusedBitCount != 0)
        {
            throw new CryptographicException("The CRL signature contains unused bits.");
        }

        var tbsReader = new AsnReader(encodedTbs, AsnEncodingRules.DER).ReadSequence();
        if (tbsReader.HasData && tbsReader.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
        {
            _ = tbsReader.ReadInteger();
        }

        var innerSignatureAlgorithm = ReadAlgorithmIdentifier(tbsReader.ReadSequence());
        if (!string.Equals(signatureAlgorithm, innerSignatureAlgorithm, StringComparison.Ordinal))
        {
            throw new CryptographicException("The CRL signature algorithms do not match.");
        }

        var issuerName = tbsReader.ReadEncodedValue();
        if (!issuerName.Span.SequenceEqual(issuer.SubjectName.RawData))
        {
            throw new CryptographicException("The CRL issuer does not match the certificate issuer.");
        }

        var thisUpdate = ReadTime(tbsReader);
        DateTimeOffset? nextUpdate = null;
        if (tbsReader.HasData && IsTime(tbsReader.PeekTag()))
        {
            nextUpdate = ReadTime(tbsReader);
        }

        var revoked = false;
        if (tbsReader.HasData && tbsReader.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence))
        {
            var revokedCertificates = tbsReader.ReadSequence();
            while (revokedCertificates.HasData)
            {
                var entry = revokedCertificates.ReadSequence();
                var serial = NormalizeInteger(entry.ReadIntegerBytes().Span);
                var revocationTime = ReadTime(entry);
                revoked |= serial.SequenceEqual(NormalizeSerial(certificate)) &&
                           revocationTime <= validationTime;
                if (entry.HasData)
                {
                    ReadExtensions(entry.ReadSequence(), entryLevel: true);
                }
                entry.ThrowIfNotEmpty();
            }
        }

        if (tbsReader.HasData)
        {
            var extensions = tbsReader.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            ReadExtensions(extensions.ReadSequence(), entryLevel: false);
            extensions.ThrowIfNotEmpty();
        }
        tbsReader.ThrowIfNotEmpty();

        if (nextUpdate is { } invalidExpiry && invalidExpiry < thisUpdate)
        {
            throw new CryptographicException("The CRL validity window is inconsistent.");
        }

        var currentEvidenceInvalid = !historical &&
            (validationTime < thisUpdate || nextUpdate is { } expiry && validationTime > expiry);
        var historicalEvidenceDoesNotCoverReference = historical &&
            thisUpdate < validationTime &&
            (nextUpdate is null || nextUpdate < validationTime);
        if (currentEvidenceInvalid || historicalEvidenceDoesNotCoverReference ||
            historical && thisUpdate > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            throw new CryptographicException("The CRL is not valid at the requested validation time.");
        }

        VerifySignature(encodedTbs.Span, signature, signatureAlgorithm, issuer);
        return new PdfRevocationEvidence(
            PdfRevocationEvidenceKind.Crl,
            revoked ? PdfTrustStatus.Invalid : PdfTrustStatus.Valid,
            source,
            thisUpdate,
            nextUpdate,
            encodedCrl.ToArray());
    }

    private static void VerifySignature(
        ReadOnlySpan<byte> tbs,
        ReadOnlySpan<byte> signature,
        string algorithmOid,
        X509Certificate2 issuer)
    {
        var hashAlgorithm = algorithmOid switch
        {
            Sha256WithRsaOid or EcdsaWithSha256Oid => HashAlgorithmName.SHA256,
            Sha384WithRsaOid or EcdsaWithSha384Oid => HashAlgorithmName.SHA384,
            Sha512WithRsaOid or EcdsaWithSha512Oid => HashAlgorithmName.SHA512,
            _ => throw new CryptographicException($"Unsupported CRL signature algorithm '{algorithmOid}'.")
        };

        var verified = algorithmOid switch
        {
            Sha256WithRsaOid or Sha384WithRsaOid or Sha512WithRsaOid =>
                issuer.GetRSAPublicKey() is { } rsa && VerifyRsa(rsa, tbs, signature, hashAlgorithm),
            _ => issuer.GetECDsaPublicKey() is { } ecdsa && VerifyEcdsa(ecdsa, tbs, signature, hashAlgorithm)
        };
        if (!verified)
        {
            throw new CryptographicException("The CRL signature is invalid.");
        }
    }

    private static bool VerifyRsa(
        RSA rsa,
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        HashAlgorithmName hashAlgorithm)
    {
        using (rsa)
        {
            return rsa.VerifyData(data, signature, hashAlgorithm, RSASignaturePadding.Pkcs1);
        }
    }

    private static bool VerifyEcdsa(
        ECDsa ecdsa,
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        HashAlgorithmName hashAlgorithm)
    {
        using (ecdsa)
        {
            return ecdsa.VerifyData(data, signature, hashAlgorithm, DSASignatureFormat.Rfc3279DerSequence);
        }
    }

    private static string ReadAlgorithmIdentifier(AsnReader reader)
    {
        var oid = reader.ReadObjectIdentifier();
        if (reader.HasData)
        {
            reader.ReadEncodedValue();
        }
        reader.ThrowIfNotEmpty();
        return oid;
    }

    private static void ReadExtensions(AsnReader extensions, bool entryLevel)
    {
        while (extensions.HasData)
        {
            var extension = extensions.ReadSequence();
            var oid = extension.ReadObjectIdentifier();
            var critical = extension.HasData &&
                           extension.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean) &&
                           extension.ReadBoolean();
            _ = extension.ReadOctetString();
            extension.ThrowIfNotEmpty();

            if (oid == DeltaCrlIndicatorOid || oid == IssuingDistributionPointOid ||
                entryLevel && oid == CertificateIssuerOid)
            {
                throw new CryptographicException("Delta, scoped, and indirect CRLs are not supported.");
            }

            var recognized = entryLevel
                ? oid is CrlReasonOid or InvalidityDateOid
                : oid is AuthorityKeyIdentifierOid or CrlNumberOid;
            if (critical && !recognized)
            {
                throw new CryptographicException($"Unsupported critical CRL extension '{oid}'.");
            }
        }
    }

    private static bool IsTime(Asn1Tag tag) =>
        tag.TagClass == TagClass.Universal &&
        tag.TagValue is (int)UniversalTagNumber.UtcTime or (int)UniversalTagNumber.GeneralizedTime;

    private static DateTimeOffset ReadTime(AsnReader reader) =>
        reader.PeekTag().TagValue == (int)UniversalTagNumber.UtcTime
            ? reader.ReadUtcTime()
            : reader.ReadGeneralizedTime();

    private static byte[] NormalizeSerial(X509Certificate2 certificate)
    {
        var serial = certificate.GetSerialNumber();
        Array.Reverse(serial);
        return NormalizeInteger(serial);
    }

    private static byte[] NormalizeInteger(ReadOnlySpan<byte> value)
    {
        var offset = 0;
        while (offset < value.Length - 1 && value[offset] == 0)
        {
            offset++;
        }
        return value[offset..].ToArray();
    }
}
