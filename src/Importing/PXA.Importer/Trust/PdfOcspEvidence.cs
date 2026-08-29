using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PXA.Importer.Trust;

public static class PdfOcspEvidence
{
    private const string BasicResponseOid = "1.3.6.1.5.5.7.48.1.1";
    private const string OcspSigningEkuOid = "1.3.6.1.5.5.7.3.9";
    private const string Sha1Oid = "1.3.14.3.2.26";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string Sha384Oid = "2.16.840.1.101.3.4.2.2";
    private const string Sha512Oid = "2.16.840.1.101.3.4.2.3";
    private const string Sha256WithRsaOid = "1.2.840.113549.1.1.11";
    private const string Sha384WithRsaOid = "1.2.840.113549.1.1.12";
    private const string Sha512WithRsaOid = "1.2.840.113549.1.1.13";
    private const string EcdsaWithSha256Oid = "1.2.840.10045.4.3.2";
    private const string EcdsaWithSha384Oid = "1.2.840.10045.4.3.3";
    private const string EcdsaWithSha512Oid = "1.2.840.10045.4.3.4";

    public static byte[] CreateRequest(X509Certificate2 certificate, X509Certificate2 issuer)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.PushSequence();
        writer.PushSequence();
        writer.PushSequence();
        writer.PushSequence();
        WriteAlgorithmIdentifier(writer, Sha256Oid);
        writer.WriteOctetString(SHA256.HashData(issuer.SubjectName.RawData));
        writer.WriteOctetString(SHA256.HashData(ReadSubjectPublicKey(issuer)));
        writer.WriteIntegerUnsigned(NormalizeSerial(certificate));
        writer.PopSequence();
        writer.PopSequence();
        writer.PopSequence();
        writer.PopSequence();
        return writer.Encode();
    }

    public static PdfRevocationEvidence ValidateResponse(
        ReadOnlyMemory<byte> encodedResponse,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        DateTimeOffset validationTime,
        TimeSpan maximumAge,
        string source) => ValidateResponseCore(
            encodedResponse,
            certificate,
            issuer,
            validationTime,
            maximumAge,
            source,
            historical: false);

    public static PdfRevocationEvidence ValidateHistoricalResponse(
        ReadOnlyMemory<byte> encodedResponse,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        DateTimeOffset trustedReferenceTime,
        TimeSpan maximumProducedAtSkew,
        string source) => ValidateResponseCore(
            encodedResponse,
            certificate,
            issuer,
            trustedReferenceTime,
            maximumProducedAtSkew,
            source,
            historical: true);

    private static PdfRevocationEvidence ValidateResponseCore(
        ReadOnlyMemory<byte> encodedResponse,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        DateTimeOffset validationTime,
        TimeSpan maximumAge,
        string source,
        bool historical)
    {
        var reader = new AsnReader(encodedResponse, AsnEncodingRules.DER);
        var response = reader.ReadSequence();
        var responseStatus = response.ReadEnumeratedValue<OcspResponseStatus>();
        if (responseStatus != OcspResponseStatus.Successful)
        {
            throw new CryptographicException($"The OCSP responder returned status '{responseStatus}'.");
        }

        var responseBytes = response.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
        var responseBytesValue = responseBytes.ReadSequence();
        if (responseBytesValue.ReadObjectIdentifier() != BasicResponseOid)
        {
            throw new CryptographicException("The OCSP response is not a basic signed response.");
        }
        var basicResponseBytes = responseBytesValue.ReadOctetString();
        responseBytesValue.ThrowIfNotEmpty();
        responseBytes.ThrowIfNotEmpty();
        response.ThrowIfNotEmpty();
        reader.ThrowIfNotEmpty();

        return ValidateBasicResponse(
            basicResponseBytes,
            encodedResponse,
            certificate,
            issuer,
            validationTime,
            maximumAge,
            source,
            historical);
    }

    private static PdfRevocationEvidence ValidateBasicResponse(
        ReadOnlyMemory<byte> basicResponseBytes,
        ReadOnlyMemory<byte> encodedResponse,
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        DateTimeOffset validationTime,
        TimeSpan maximumAge,
        string source,
        bool historical)
    {
        var basicReader = new AsnReader(basicResponseBytes, AsnEncodingRules.DER);
        var basicResponse = basicReader.ReadSequence();
        var encodedResponseData = basicResponse.ReadEncodedValue();
        var signatureAlgorithm = ReadAlgorithmIdentifier(basicResponse.ReadSequence());
        var signature = basicResponse.ReadBitString(out var unusedBits);
        if (unusedBits != 0)
        {
            throw new CryptographicException("The OCSP signature contains unused bits.");
        }

        var includedCertificates = new List<X509Certificate2>();
        if (basicResponse.HasData)
        {
            var certificateWrapper = basicResponse.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            var certificates = certificateWrapper.ReadSequence();
            while (certificates.HasData)
            {
                includedCertificates.Add(X509CertificateLoader.LoadCertificate(certificates.ReadEncodedValue().Span));
            }
            certificateWrapper.ThrowIfNotEmpty();
        }
        basicResponse.ThrowIfNotEmpty();
        basicReader.ThrowIfNotEmpty();

        try
        {
            var responseData = new AsnReader(encodedResponseData, AsnEncodingRules.DER).ReadSequence();
            if (responseData.HasData &&
                responseData.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
            {
                var version = responseData.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
                _ = version.ReadInteger();
                version.ThrowIfNotEmpty();
            }

            var responderId = responseData.ReadEncodedValue();
            var producedAt = responseData.ReadGeneralizedTime();
            var responses = responseData.ReadSequence();
            PdfRevocationEvidence? matchingEvidence = null;
            while (responses.HasData)
            {
                var singleResponse = responses.ReadSequence();
                var matchesCertificate = ReadAndMatchCertificateId(singleResponse.ReadSequence(), certificate, issuer);
                var status = ReadCertificateStatus(singleResponse, validationTime, historical);
                var thisUpdate = singleResponse.ReadGeneralizedTime();
                DateTimeOffset? nextUpdate = null;
                if (singleResponse.HasData &&
                    singleResponse.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
                {
                    var nextUpdateValue = singleResponse.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
                    nextUpdate = nextUpdateValue.ReadGeneralizedTime();
                    nextUpdateValue.ThrowIfNotEmpty();
                }
                if (singleResponse.HasData)
                {
                    throw new CryptographicException("OCSP single-response extensions are not supported.");
                }

                if (matchesCertificate)
                {
                    matchingEvidence = new PdfRevocationEvidence(
                        PdfRevocationEvidenceKind.Ocsp,
                        status,
                        source,
                        thisUpdate,
                        nextUpdate,
                        encodedResponse.ToArray());
                }
            }

            if (responseData.HasData)
            {
                throw new CryptographicException("OCSP response extensions are not supported.");
            }
            responseData.ThrowIfNotEmpty();
            var evidence = matchingEvidence ??
                throw new CryptographicException("The OCSP response does not contain the requested certificate ID.");
            var currentEvidenceInvalid = !historical &&
                (producedAt > validationTime.AddMinutes(5) ||
                 producedAt < validationTime.Subtract(maximumAge) ||
                 evidence.ThisUpdate > validationTime.AddMinutes(5) ||
                 evidence.ThisUpdate < validationTime.Subtract(maximumAge) ||
                 evidence.NextUpdate is { } evidenceExpiry && evidenceExpiry < validationTime);
            var historicalEvidenceInvalid = historical &&
                (producedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
                 evidence.ThisUpdate > DateTimeOffset.UtcNow.AddMinutes(5) ||
                 (producedAt - evidence.ThisUpdate).Duration() > maximumAge ||
                 evidence.NextUpdate is { } invalidExpiry && invalidExpiry < evidence.ThisUpdate ||
                 evidence.ThisUpdate < validationTime &&
                 (evidence.NextUpdate is null || evidence.NextUpdate < validationTime));
            if (currentEvidenceInvalid || historicalEvidenceInvalid)
            {
                throw new CryptographicException("The OCSP response is stale or not yet valid.");
            }

            var responderCertificate = SelectResponderCertificate(responderId, includedCertificates, issuer);
            ValidateResponderCertificate(responderCertificate, issuer, producedAt);
            VerifySignature(encodedResponseData.Span, signature, signatureAlgorithm, responderCertificate);
            return evidence;
        }
        finally
        {
            foreach (var includedCertificate in includedCertificates)
            {
                includedCertificate.Dispose();
            }
        }
    }

    private static bool ReadAndMatchCertificateId(
        AsnReader certificateId,
        X509Certificate2 certificate,
        X509Certificate2 issuer)
    {
        var hashAlgorithm = GetHashAlgorithm(ReadAlgorithmIdentifier(certificateId.ReadSequence()));
        var issuerNameHash = certificateId.ReadOctetString();
        var issuerKeyHash = certificateId.ReadOctetString();
        var serial = NormalizeInteger(certificateId.ReadIntegerBytes().Span);
        certificateId.ThrowIfNotEmpty();
        return CryptographicOperations.FixedTimeEquals(
                   issuerNameHash,
                   Hash(hashAlgorithm, issuer.SubjectName.RawData)) &&
               CryptographicOperations.FixedTimeEquals(
                   issuerKeyHash,
                   Hash(hashAlgorithm, ReadSubjectPublicKey(issuer))) &&
               serial.SequenceEqual(NormalizeSerial(certificate));
    }

    private static PdfTrustStatus ReadCertificateStatus(
        AsnReader response,
        DateTimeOffset validationTime,
        bool historical)
    {
        var tag = response.PeekTag();
        if (tag.HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            response.ReadNull(new Asn1Tag(TagClass.ContextSpecific, 0));
            return PdfTrustStatus.Valid;
        }
        if (tag.HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 2)))
        {
            response.ReadNull(new Asn1Tag(TagClass.ContextSpecific, 2));
            return PdfTrustStatus.Indeterminate;
        }
        if (!tag.HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, 1, true)))
        {
            throw new CryptographicException("The OCSP certificate status is invalid.");
        }

        var revokedInfo = response.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 1, true));
        var revocationTime = revokedInfo.ReadGeneralizedTime();
        if (revokedInfo.HasData)
        {
            revokedInfo.ReadEncodedValue();
        }
        revokedInfo.ThrowIfNotEmpty();
        return revocationTime <= validationTime
            ? PdfTrustStatus.Invalid
            : historical
                ? PdfTrustStatus.Valid
                : PdfTrustStatus.Indeterminate;
    }

    private static X509Certificate2 SelectResponderCertificate(
        ReadOnlyMemory<byte> responderId,
        IReadOnlyList<X509Certificate2> includedCertificates,
        X509Certificate2 issuer)
    {
        var candidates = includedCertificates.Concat([issuer]);
        var tag = new AsnReader(responderId, AsnEncodingRules.DER).PeekTag();
        if (tag.TagClass != TagClass.ContextSpecific)
        {
            throw new CryptographicException("The OCSP responder ID is invalid.");
        }

        foreach (var candidate in candidates)
        {
            if (tag.TagValue == 2)
            {
                var idReader = new AsnReader(responderId, AsnEncodingRules.DER);
                var keyHash = idReader.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 2));
                if (CryptographicOperations.FixedTimeEquals(keyHash, SHA1.HashData(ReadSubjectPublicKey(candidate))))
                {
                    return candidate;
                }
            }
            else if (tag.TagValue == 1 && MatchesResponderName(responderId, candidate))
            {
                return candidate;
            }
        }
        throw new CryptographicException("The OCSP responder certificate is missing.");
    }

    private static bool MatchesResponderName(ReadOnlyMemory<byte> encodedId, X509Certificate2 certificate)
    {
        try
        {
            var reader = new AsnReader(encodedId, AsnEncodingRules.DER);
            var wrapper = reader.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 1, true));
            var encodedName = wrapper.ReadEncodedValue();
            wrapper.ThrowIfNotEmpty();
            reader.ThrowIfNotEmpty();
            return encodedName.Span.SequenceEqual(certificate.SubjectName.RawData);
        }
        catch (AsnContentException)
        {
            var encodedName = encodedId.ToArray();
            if (encodedName.Length == 0 || encodedName[0] != 0xA1)
            {
                return false;
            }
            encodedName[0] = 0x30;
            return encodedName.AsSpan().SequenceEqual(certificate.SubjectName.RawData);
        }
    }

    private static void ValidateResponderCertificate(
        X509Certificate2 responder,
        X509Certificate2 issuer,
        DateTimeOffset producedAt)
    {
        if (producedAt.UtcDateTime < responder.NotBefore.ToUniversalTime() ||
            producedAt.UtcDateTime > responder.NotAfter.ToUniversalTime())
        {
            throw new CryptographicException("The OCSP responder certificate is not valid at the response time.");
        }

        if (string.Equals(responder.Thumbprint, issuer.Thumbprint, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var eku = responder.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        if (eku is null || !eku.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == OcspSigningEkuOid))
        {
            throw new CryptographicException("The delegated OCSP responder lacks the OCSP signing EKU.");
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(issuer);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.DisableCertificateDownloads = true;
        chain.ChainPolicy.VerificationTime = producedAt.UtcDateTime;
        if (!chain.Build(responder))
        {
            throw new CryptographicException("The delegated OCSP responder is not issued by the certificate issuer.");
        }
    }

    private static void VerifySignature(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        string algorithmOid,
        X509Certificate2 responder)
    {
        var hashAlgorithm = algorithmOid switch
        {
            Sha256WithRsaOid or EcdsaWithSha256Oid => HashAlgorithmName.SHA256,
            Sha384WithRsaOid or EcdsaWithSha384Oid => HashAlgorithmName.SHA384,
            Sha512WithRsaOid or EcdsaWithSha512Oid => HashAlgorithmName.SHA512,
            _ => throw new CryptographicException($"Unsupported OCSP signature algorithm '{algorithmOid}'.")
        };
        var valid = algorithmOid switch
        {
            Sha256WithRsaOid or Sha384WithRsaOid or Sha512WithRsaOid =>
                responder.GetRSAPublicKey() is { } rsa && VerifyRsa(rsa, data, signature, hashAlgorithm),
            _ => responder.GetECDsaPublicKey() is { } ecdsa && VerifyEcdsa(ecdsa, data, signature, hashAlgorithm)
        };
        if (!valid)
        {
            throw new CryptographicException("The OCSP response signature is invalid.");
        }
    }

    private static bool VerifyRsa(RSA rsa, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, HashAlgorithmName hash)
    {
        using (rsa)
        {
            return rsa.VerifyData(data, signature, hash, RSASignaturePadding.Pkcs1);
        }
    }

    private static bool VerifyEcdsa(ECDsa ecdsa, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, HashAlgorithmName hash)
    {
        using (ecdsa)
        {
            return ecdsa.VerifyData(data, signature, hash, DSASignatureFormat.Rfc3279DerSequence);
        }
    }

    private static byte[] ReadSubjectPublicKey(X509Certificate2 certificate)
    {
        var reader = new AsnReader(certificate.PublicKey.ExportSubjectPublicKeyInfo(), AsnEncodingRules.DER);
        var subjectPublicKeyInfo = reader.ReadSequence();
        subjectPublicKeyInfo.ReadSequence().ReadEncodedValue();
        var key = subjectPublicKeyInfo.ReadBitString(out var unusedBits);
        if (unusedBits != 0)
        {
            throw new CryptographicException("The issuer public key contains unused bits.");
        }
        subjectPublicKeyInfo.ThrowIfNotEmpty();
        reader.ThrowIfNotEmpty();
        return key;
    }

    private static void WriteAlgorithmIdentifier(AsnWriter writer, string oid)
    {
        writer.PushSequence();
        writer.WriteObjectIdentifier(oid);
        writer.WriteNull();
        writer.PopSequence();
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

    private static HashAlgorithmName GetHashAlgorithm(string oid) => oid switch
    {
        Sha1Oid => HashAlgorithmName.SHA1,
        Sha256Oid => HashAlgorithmName.SHA256,
        Sha384Oid => HashAlgorithmName.SHA384,
        Sha512Oid => HashAlgorithmName.SHA512,
        _ => throw new CryptographicException($"Unsupported OCSP CertID hash algorithm '{oid}'.")
    };

    private static byte[] Hash(HashAlgorithmName algorithm, ReadOnlySpan<byte> value) => algorithm.Name switch
    {
        "SHA1" => SHA1.HashData(value),
        "SHA256" => SHA256.HashData(value),
        "SHA384" => SHA384.HashData(value),
        "SHA512" => SHA512.HashData(value),
        _ => throw new CryptographicException($"Unsupported hash algorithm '{algorithm.Name}'.")
    };

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

    private enum OcspResponseStatus
    {
        Successful = 0,
        MalformedRequest = 1,
        InternalError = 2,
        TryLater = 3,
        SignatureRequired = 5,
        Unauthorized = 6
    }
}
