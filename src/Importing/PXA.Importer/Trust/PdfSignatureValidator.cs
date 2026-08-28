using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PXA.Importer.Objects;
using PXA.Importer.Xref;

namespace PXA.Importer.Trust;

internal sealed record PdfSignatureValidationResult(
    PdfSignatureInspection Inspection,
    IReadOnlyList<PdfTrustDiagnostic> Diagnostics);

internal static class PdfSignatureValidator
{
    private const string SignedDataOid = "1.2.840.113549.1.7.2";
    private const string MessageDigestAttributeOid = "1.2.840.113549.1.9.4";
    private const string SigningTimeAttributeOid = "1.2.840.113549.1.9.5";
    private const string SigningCertificateV2AttributeOid = "1.2.840.113549.1.9.16.2.47";
    private const string SignatureTimestampTokenAttributeOid = "1.2.840.113549.1.9.16.2.14";
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string Sha384Oid = "2.16.840.1.101.3.4.2.2";
    private const string Sha512Oid = "2.16.840.1.101.3.4.2.3";
    private const string RsaEncryptionOid = "1.2.840.113549.1.1.1";
    private const string Sha256WithRsaOid = "1.2.840.113549.1.1.11";
    private const string Sha384WithRsaOid = "1.2.840.113549.1.1.12";
    private const string Sha512WithRsaOid = "1.2.840.113549.1.1.13";
    private const string TimestampingExtendedKeyUsageOid = "1.3.6.1.5.5.7.3.8";

    public static PdfSignatureValidationResult Validate(
        ReadOnlySpan<byte> source,
        PdfObjectId signatureId,
        PdfDictionary signature,
        IReadOnlyList<PdfCrossReferenceRevision> revisions,
        PdfSignatureValidationPolicy validationPolicy)
    {
        var diagnostics = new List<PdfTrustDiagnostic>();
        var subFilter = ReadName(signature["SubFilter"]);
        var byteRange = ReadIntegerArray(signature["ByteRange"]);
        var contents = signature["Contents"] as PdfString;
        var baseInspection = new PdfSignatureInspection(
            signatureId.Number,
            signatureId.Generation,
            PdfTrustStatus.Indeterminate,
            subFilter,
            byteRange,
            contents?.GetDecodedBytes().Length);

        if (!TryValidateByteRange(source, byteRange, revisions, out var signedContent, out var coversWholeDocument,
                out var signedRevisionEnd, out var coverageMessage))
        {
            diagnostics.Add(Diagnostic(
                "PXAPDFTRUST102",
                PdfTrustDiagnosticSeverity.Error,
                coverageMessage,
                signatureId));
            return new PdfSignatureValidationResult(
                baseInspection with
                {
                    Status = PdfTrustStatus.Invalid,
                    IntegrityStatus = PdfTrustStatus.Invalid,
                    CoverageStatus = PdfTrustStatus.Invalid,
                    SignedRevisionEndOffset = signedRevisionEnd
                },
                diagnostics);
        }

        if (contents is null || !TryTrimDer(contents.GetDecodedBytes().Span, out var cms))
        {
            diagnostics.Add(Diagnostic(
                "PXAPDFTRUST103",
                PdfTrustDiagnosticSeverity.Error,
                "The signature /Contents value does not contain a bounded DER CMS object.",
                signatureId));
            return new PdfSignatureValidationResult(
                baseInspection with
                {
                    Status = PdfTrustStatus.Invalid,
                    IntegrityStatus = PdfTrustStatus.Invalid,
                    CoverageStatus = PdfTrustStatus.Valid,
                    CoversWholeDocument = coversWholeDocument,
                    SignedRevisionEndOffset = signedRevisionEnd
                },
                diagnostics);
        }

        try
        {
            var parsed = ParseCms(cms);
            if (!TryGetHashAlgorithm(parsed.DigestAlgorithmOid, out var hashAlgorithm, out var digestName))
            {
                diagnostics.Add(Diagnostic(
                    "PXAPDFTRUST104",
                    PdfTrustDiagnosticSeverity.Warning,
                    $"The CMS digest algorithm '{parsed.DigestAlgorithmOid}' is not supported.",
                    signatureId));
                return new PdfSignatureValidationResult(
                    baseInspection with
                    {
                        Status = PdfTrustStatus.Unsupported,
                        IntegrityStatus = PdfTrustStatus.Unsupported,
                        CoverageStatus = PdfTrustStatus.Valid,
                        DigestAlgorithm = parsed.DigestAlgorithmOid,
                        CoversWholeDocument = coversWholeDocument,
                        SignedRevisionEndOffset = signedRevisionEnd
                    },
                    diagnostics);
            }

            var contentDigest = HashData(hashAlgorithm, signedContent);
            var messageDigestMatches = parsed.MessageDigest is not null &&
                CryptographicOperations.FixedTimeEquals(contentDigest, parsed.MessageDigest);
            var certificate = SelectSignerCertificate(parsed);
            var signingCertificateMatches = certificate is not null &&
                (parsed.SigningCertificateV2Hash is null ||
                 CryptographicOperations.FixedTimeEquals(
                     SHA256.HashData(certificate.RawData),
                     parsed.SigningCertificateV2Hash));
            var signatureMatches = certificate is not null &&
                VerifySignature(certificate, parsed, hashAlgorithm);
            var timestamp = ValidateTimestamp(parsed, validationPolicy);
            var integrityStatus = messageDigestMatches && signingCertificateMatches && signatureMatches &&
                                  timestamp.Status != PdfTrustStatus.Invalid
                ? PdfTrustStatus.Valid
                : PdfTrustStatus.Invalid;
            var certificateValidation = certificate is null
                ? new CertificateValidation(PdfTrustStatus.Invalid, PdfTrustStatus.NotApplicable)
                : ValidateCertificate(certificate, parsed.Certificates, parsed.SigningTime, validationPolicy);
            var certificateStatus = certificateValidation.TrustStatus;
            var status = integrityStatus == PdfTrustStatus.Invalid ||
                         certificateValidation.RevocationStatus == PdfTrustStatus.Invalid
                ? PdfTrustStatus.Invalid
                : certificateStatus == PdfTrustStatus.Valid &&
                  certificateValidation.RevocationStatus is PdfTrustStatus.Valid or PdfTrustStatus.NotApplicable
                    ? PdfTrustStatus.Valid
                    : PdfTrustStatus.Indeterminate;

            diagnostics.Add(Diagnostic(
                integrityStatus == PdfTrustStatus.Valid ? "PXAPDFTRUST110" : "PXAPDFTRUST105",
                integrityStatus == PdfTrustStatus.Valid
                    ? PdfTrustDiagnosticSeverity.Info
                    : PdfTrustDiagnosticSeverity.Error,
                integrityStatus == PdfTrustStatus.Valid
                    ? "The detached CMS signature and signed byte ranges are cryptographically valid."
                    : "The detached CMS signature, document digest, or signing-certificate binding is invalid.",
                signatureId));
            if (!coversWholeDocument)
            {
                diagnostics.Add(Diagnostic(
                    "PXAPDFTRUST111",
                    PdfTrustDiagnosticSeverity.Info,
                    "The signature covers an earlier complete PDF revision; later incremental revisions exist.",
                    signatureId));
            }

            if (certificateStatus == PdfTrustStatus.Indeterminate)
            {
                diagnostics.Add(Diagnostic(
                    "PXAPDFTRUST112",
                    PdfTrustDiagnosticSeverity.Warning,
                    "The signer certificate is cryptographically bound, but its chain is not trusted by the current host trust store.",
                    signatureId));
            }

            if (certificateValidation.RevocationStatus != PdfTrustStatus.NotApplicable)
            {
                diagnostics.Add(Diagnostic(
                    certificateValidation.RevocationStatus == PdfTrustStatus.Invalid
                        ? "PXAPDFTRUST116"
                        : certificateValidation.RevocationStatus == PdfTrustStatus.Valid
                            ? "PXAPDFTRUST115"
                            : "PXAPDFTRUST117",
                    certificateValidation.RevocationStatus == PdfTrustStatus.Invalid
                        ? PdfTrustDiagnosticSeverity.Error
                        : certificateValidation.RevocationStatus == PdfTrustStatus.Valid
                            ? PdfTrustDiagnosticSeverity.Info
                            : PdfTrustDiagnosticSeverity.Warning,
                    certificateValidation.RevocationStatus switch
                    {
                        PdfTrustStatus.Valid => "The signer certificate passed the configured revocation policy.",
                        PdfTrustStatus.Invalid => "The signer certificate is revoked under the configured revocation policy.",
                        _ => "The signer certificate revocation status could not be established under the configured policy."
                    },
                    signatureId));
            }

            if (timestamp.Status != PdfTrustStatus.NotApplicable)
            {
                diagnostics.Add(Diagnostic(
                    timestamp.Status == PdfTrustStatus.Invalid ? "PXAPDFTRUST114" : "PXAPDFTRUST113",
                    timestamp.Status == PdfTrustStatus.Invalid
                        ? PdfTrustDiagnosticSeverity.Error
                        : timestamp.Status == PdfTrustStatus.Valid
                            ? PdfTrustDiagnosticSeverity.Info
                            : PdfTrustDiagnosticSeverity.Warning,
                    timestamp.Status switch
                    {
                        PdfTrustStatus.Valid => "The RFC 3161 signature timestamp and TSA certificate chain are valid.",
                        PdfTrustStatus.Indeterminate => "The RFC 3161 signature timestamp is cryptographically valid, but its TSA chain is not trusted by the current host trust store.",
                        _ => "The RFC 3161 signature timestamp, TSA certificate, or configured revocation result is invalid."
                    },
                    signatureId));
            }

            if (timestamp.RevocationStatus != PdfTrustStatus.NotApplicable)
            {
                diagnostics.Add(Diagnostic(
                    timestamp.RevocationStatus == PdfTrustStatus.Invalid
                        ? "PXAPDFTRUST119"
                        : timestamp.RevocationStatus == PdfTrustStatus.Valid
                            ? "PXAPDFTRUST118"
                            : "PXAPDFTRUST120",
                    timestamp.RevocationStatus == PdfTrustStatus.Invalid
                        ? PdfTrustDiagnosticSeverity.Error
                        : timestamp.RevocationStatus == PdfTrustStatus.Valid
                            ? PdfTrustDiagnosticSeverity.Info
                            : PdfTrustDiagnosticSeverity.Warning,
                    timestamp.RevocationStatus switch
                    {
                        PdfTrustStatus.Valid => "The TSA certificate passed the configured revocation policy.",
                        PdfTrustStatus.Invalid => "The TSA certificate is revoked under the configured revocation policy.",
                        _ => "The TSA certificate revocation status could not be established under the configured policy."
                    },
                    signatureId));
            }

            return new PdfSignatureValidationResult(
                baseInspection with
                {
                    Status = status,
                    IntegrityStatus = integrityStatus,
                    CoverageStatus = PdfTrustStatus.Valid,
                    CertificateStatus = certificateStatus,
                    RevocationStatus = certificateValidation.RevocationStatus,
                    PadesProfile = DescribePadesProfile(subFilter, parsed),
                    DigestAlgorithm = digestName,
                    SignerSubject = certificate?.Subject,
                    SignerThumbprint = certificate?.Thumbprint,
                    SigningTime = parsed.SigningTime,
                    CoversWholeDocument = coversWholeDocument,
                    SignedRevisionEndOffset = signedRevisionEnd,
                    TimestampStatus = timestamp.Status,
                    TimestampRevocationStatus = timestamp.RevocationStatus,
                    TimestampTime = timestamp.Time,
                    TimestampAuthoritySubject = timestamp.AuthoritySubject
                },
                diagnostics);
        }
        catch (Exception exception) when (exception is AsnContentException or CryptographicException or InvalidDataException)
        {
            diagnostics.Add(Diagnostic(
                "PXAPDFTRUST106",
                PdfTrustDiagnosticSeverity.Error,
                $"The CMS signature could not be decoded or verified: {exception.GetType().Name}.",
                signatureId));
            return new PdfSignatureValidationResult(
                baseInspection with
                {
                    Status = PdfTrustStatus.Invalid,
                    IntegrityStatus = PdfTrustStatus.Invalid,
                    CoverageStatus = PdfTrustStatus.Valid,
                    CoversWholeDocument = coversWholeDocument,
                    SignedRevisionEndOffset = signedRevisionEnd
                },
                diagnostics);
        }
    }

    private static bool TryValidateByteRange(
        ReadOnlySpan<byte> source,
        IReadOnlyList<long> byteRange,
        IReadOnlyList<PdfCrossReferenceRevision> revisions,
        out byte[] signedContent,
        out bool coversWholeDocument,
        out long? signedRevisionEnd,
        out string message)
    {
        signedContent = [];
        coversWholeDocument = false;
        signedRevisionEnd = null;
        if (byteRange.Count != 4 || byteRange[0] != 0 || byteRange.Any(static value => value < 0))
        {
            message = "The signature /ByteRange must contain two non-negative ranges starting at byte zero.";
            return false;
        }

        var firstLength = byteRange[1];
        var secondStart = byteRange[2];
        var secondLength = byteRange[3];
        var secondEnd = secondStart + secondLength;
        if (firstLength > secondStart || secondEnd < secondStart || secondEnd > source.Length)
        {
            message = "The signature /ByteRange overlaps, overflows, or points outside the PDF.";
            return false;
        }

        signedRevisionEnd = secondEnd;
        coversWholeDocument = secondEnd == source.Length;
        var matchesRevisionBoundary = coversWholeDocument || revisions.Any(revision => revision.EndOffset == secondEnd);
        if (!matchesRevisionBoundary)
        {
            message = "The signature does not end at a known complete PDF revision boundary.";
            return false;
        }

        var gap = source.Slice(checked((int)firstLength), checked((int)(secondStart - firstLength)));
        if (gap.Length < 2 || gap[0] != (byte)'<' || gap[^1] != (byte)'>')
        {
            message = "The unsigned /ByteRange gap is not limited to one hexadecimal signature value.";
            return false;
        }

        var first = source[..checked((int)firstLength)];
        var second = source.Slice(checked((int)secondStart), checked((int)secondLength));
        signedContent = new byte[first.Length + second.Length];
        first.CopyTo(signedContent);
        second.CopyTo(signedContent.AsSpan(first.Length));
        message = string.Empty;
        return true;
    }

    private static ParsedCms ParseCms(ReadOnlySpan<byte> cms)
    {
        var contentInfo = new AsnReader(cms.ToArray(), AsnEncodingRules.BER).ReadSequence();
        if (contentInfo.ReadObjectIdentifier() != SignedDataOid)
        {
            throw new InvalidDataException("The CMS content type is not SignedData.");
        }

        var explicitContent = contentInfo.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
        var signedData = explicitContent.ReadSequence();
        signedData.ReadInteger();
        var digestAlgorithms = signedData.ReadSetOf();
        var digestAlgorithmOid = ReadAlgorithmIdentifier(digestAlgorithms.ReadSequence());
        signedData.ReadSequence();

        var certificates = new List<X509Certificate2>();
        var certificateTag = new Asn1Tag(TagClass.ContextSpecific, 0, true);
        if (signedData.HasData && signedData.PeekTag().HasSameClassAndValue(certificateTag))
        {
            var certificateSet = signedData.ReadSetOf(certificateTag);
            while (certificateSet.HasData)
            {
                var encoded = certificateSet.ReadEncodedValue();
                if (encoded.Span.Length > 0 && encoded.Span[0] == 0x30)
                {
                    certificates.Add(X509CertificateLoader.LoadCertificate(encoded.Span));
                }
            }
        }

        var crlTag = new Asn1Tag(TagClass.ContextSpecific, 1, true);
        if (signedData.HasData && signedData.PeekTag().HasSameClassAndValue(crlTag))
        {
            signedData.ReadEncodedValue();
        }

        var signerInfos = signedData.ReadSetOf();
        if (!signerInfos.HasData)
        {
            throw new InvalidDataException("The CMS does not contain a signer.");
        }

        var signerInfo = signerInfos.ReadSequence();
        signerInfo.ReadInteger();
        signerInfo.ReadEncodedValue();
        var signerDigestOid = ReadAlgorithmIdentifier(signerInfo.ReadSequence());
        var signedAttributesTag = new Asn1Tag(TagClass.ContextSpecific, 0, true);
        if (!signerInfo.HasData || !signerInfo.PeekTag().HasSameClassAndValue(signedAttributesTag))
        {
            throw new InvalidDataException("The CMS signer has no signed attributes.");
        }

        var signedAttributes = signerInfo.ReadEncodedValue().ToArray();
        signedAttributes[0] = 0x31;
        var attributes = ReadAttributes(signedAttributes);
        var signatureAlgorithmOid = ReadAlgorithmIdentifier(signerInfo.ReadSequence());
        var signatureValue = signerInfo.ReadOctetString();
        byte[]? timestampToken = null;
        var unsignedAttributesTag = new Asn1Tag(TagClass.ContextSpecific, 1, true);
        if (signerInfo.HasData && signerInfo.PeekTag().HasSameClassAndValue(unsignedAttributesTag))
        {
            var encodedUnsignedAttributes = signerInfo.ReadEncodedValue().ToArray();
            encodedUnsignedAttributes[0] = 0x31;
            timestampToken = ReadTimestampToken(encodedUnsignedAttributes);
        }

        return new ParsedCms(
            signerDigestOid ?? digestAlgorithmOid,
            signatureAlgorithmOid,
            signedAttributes,
            signatureValue,
            attributes.MessageDigest,
            attributes.SigningCertificateV2Hash,
            attributes.SigningTime,
            certificates,
            timestampToken);
    }

    private static byte[]? ReadTimestampToken(byte[] encodedUnsignedAttributes)
    {
        var attributes = new AsnReader(encodedUnsignedAttributes, AsnEncodingRules.BER).ReadSetOf();
        while (attributes.HasData)
        {
            var attribute = attributes.ReadSequence();
            var oid = attribute.ReadObjectIdentifier();
            var values = attribute.ReadSetOf();
            if (oid == SignatureTimestampTokenAttributeOid)
            {
                return values.ReadEncodedValue().ToArray();
            }
        }

        return null;
    }

    private static ParsedAttributes ReadAttributes(byte[] encodedAttributes)
    {
        byte[]? messageDigest = null;
        byte[]? signingCertificateV2Hash = null;
        DateTimeOffset? signingTime = null;
        var attributes = new AsnReader(encodedAttributes, AsnEncodingRules.BER).ReadSetOf();
        while (attributes.HasData)
        {
            var attribute = attributes.ReadSequence();
            var oid = attribute.ReadObjectIdentifier();
            var values = attribute.ReadSetOf();
            switch (oid)
            {
                case MessageDigestAttributeOid:
                    messageDigest = values.ReadOctetString();
                    break;
                case SigningTimeAttributeOid:
                    signingTime = values.PeekTag().TagValue == (int)UniversalTagNumber.UtcTime
                        ? values.ReadUtcTime()
                        : values.ReadGeneralizedTime();
                    break;
                case SigningCertificateV2AttributeOid:
                    var signingCertificate = values.ReadSequence();
                    var certificateIds = signingCertificate.ReadSequence();
                    var certificateId = certificateIds.ReadSequence();
                    if (certificateId.PeekTag().TagValue == (int)UniversalTagNumber.Sequence)
                    {
                        certificateId.ReadSequence();
                    }

                    signingCertificateV2Hash = certificateId.ReadOctetString();
                    break;
            }
        }

        return new ParsedAttributes(messageDigest, signingCertificateV2Hash, signingTime);
    }

    private static X509Certificate2? SelectSignerCertificate(ParsedCms cms)
    {
        if (cms.SigningCertificateV2Hash is not null)
        {
            return cms.Certificates.FirstOrDefault(certificate =>
                CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(certificate.RawData),
                    cms.SigningCertificateV2Hash));
        }

        return cms.Certificates.FirstOrDefault();
    }

    private static bool VerifySignature(
        X509Certificate2 certificate,
        ParsedCms cms,
        HashAlgorithmName hashAlgorithm)
    {
        if (cms.SignatureAlgorithmOid is not (RsaEncryptionOid or Sha256WithRsaOid or Sha384WithRsaOid or Sha512WithRsaOid))
        {
            return false;
        }

        using var rsa = certificate.GetRSAPublicKey();
        return rsa is not null && rsa.VerifyHash(
            HashData(hashAlgorithm, cms.SignedAttributes),
            cms.Signature,
            hashAlgorithm,
            RSASignaturePadding.Pkcs1);
    }

    private static CertificateValidation ValidateCertificate(
        X509Certificate2 certificate,
        IReadOnlyList<X509Certificate2> certificates,
        DateTimeOffset? signingTime,
        PdfSignatureValidationPolicy policy)
    {
        var validationTime = signingTime ?? DateTimeOffset.UtcNow;
        if (validationTime.UtcDateTime < certificate.NotBefore.ToUniversalTime() ||
            validationTime.UtcDateTime > certificate.NotAfter.ToUniversalTime())
        {
            return new CertificateValidation(PdfTrustStatus.Invalid, PdfTrustStatus.NotApplicable);
        }

        var trustModes = GetTrustModes(policy);
        var trustStatuses = trustModes.Select(useCustomTrust =>
        {
            using var chain = CreateChain(
                policy,
                certificate,
                certificates,
                validationTime,
                X509RevocationMode.NoCheck,
                useCustomTrust);
            return chain.Build(certificate)
                ? PdfTrustStatus.Valid
                : ClassifyTrustFailure(chain);
        }).ToArray();
        var trustStatus = trustStatuses.Contains(PdfTrustStatus.Valid)
            ? PdfTrustStatus.Valid
            : trustStatuses.Contains(PdfTrustStatus.Indeterminate)
                ? PdfTrustStatus.Indeterminate
                : PdfTrustStatus.Invalid;
        if (policy.RevocationMode == PdfCertificateRevocationMode.NoCheck)
        {
            return new CertificateValidation(trustStatus, PdfTrustStatus.NotApplicable);
        }

        var revocationMode = policy.RevocationMode == PdfCertificateRevocationMode.Online
            ? X509RevocationMode.Online
            : X509RevocationMode.Offline;
        var revocationStatuses = trustModes.Select(useCustomTrust =>
        {
            using var chain = CreateChain(
                policy,
                certificate,
                certificates,
                validationTime,
                revocationMode,
                useCustomTrust);
            return chain.Build(certificate)
                ? PdfTrustStatus.Valid
                : ClassifyRevocationFailure(chain);
        }).ToArray();
        var revocationStatus = revocationStatuses.Contains(PdfTrustStatus.Invalid)
            ? PdfTrustStatus.Invalid
            : revocationStatuses.Contains(PdfTrustStatus.Valid)
                ? PdfTrustStatus.Valid
                : PdfTrustStatus.Indeterminate;
        return new CertificateValidation(trustStatus, revocationStatus);
    }

    private static IReadOnlyList<bool> GetTrustModes(PdfSignatureValidationPolicy policy)
    {
        var modes = new List<bool>(2);
        if (policy.UseSystemTrustStore)
        {
            modes.Add(false);
        }

        if (policy.CustomTrustAnchors.Count > 0 || !policy.UseSystemTrustStore)
        {
            modes.Add(true);
        }

        return modes;
    }

    private static X509Chain CreateChain(
        PdfSignatureValidationPolicy policy,
        X509Certificate2 certificate,
        IReadOnlyList<X509Certificate2> certificates,
        DateTimeOffset validationTime,
        X509RevocationMode revocationMode,
        bool useCustomTrust)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = revocationMode;
        chain.ChainPolicy.RevocationFlag = policy.RevocationFlag;
        chain.ChainPolicy.VerificationTime = validationTime.UtcDateTime;
        chain.ChainPolicy.DisableCertificateDownloads = !policy.AllowCertificateDownloads;
        chain.ChainPolicy.UrlRetrievalTimeout = policy.NetworkTimeout;
        foreach (var candidate in certificates
                     .Concat(policy.IntermediateCertificates)
                     .Where(candidate => candidate.Thumbprint != certificate.Thumbprint))
        {
            chain.ChainPolicy.ExtraStore.Add(candidate);
        }

        if (useCustomTrust)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(policy.CustomTrustAnchors.ToArray());
        }

        return chain;
    }

    private static PdfTrustStatus ClassifyTrustFailure(X509Chain chain)
    {
        var flags = chain.ChainStatus.Aggregate(
            X509ChainStatusFlags.NoError,
            static (current, status) => current | status.Status);
        var onlyTrustAnchorMissing = (flags & ~(X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.PartialChain)) == 0;
        return onlyTrustAnchorMissing ? PdfTrustStatus.Indeterminate : PdfTrustStatus.Invalid;
    }

    private static PdfTrustStatus ClassifyRevocationFailure(X509Chain chain)
    {
        var flags = chain.ChainStatus.Aggregate(
            X509ChainStatusFlags.NoError,
            static (current, status) => current | status.Status);
        if ((flags & X509ChainStatusFlags.Revoked) != 0)
        {
            return PdfTrustStatus.Invalid;
        }

        return PdfTrustStatus.Indeterminate;
    }

    private static TimestampValidation ValidateTimestamp(
        ParsedCms cms,
        PdfSignatureValidationPolicy validationPolicy)
    {
        if (cms.TimestampToken is null)
        {
            return new TimestampValidation(PdfTrustStatus.NotApplicable, PdfTrustStatus.NotApplicable, null, null);
        }

        try
        {
            if (!Rfc3161TimestampToken.TryDecode(cms.TimestampToken, out var token, out var bytesConsumed) ||
                token is null || bytesConsumed != cms.TimestampToken.Length ||
                !TryGetHashAlgorithm(token.TokenInfo.HashAlgorithmId.Value ?? string.Empty, out var hashAlgorithm, out _))
            {
                return new TimestampValidation(PdfTrustStatus.Invalid, PdfTrustStatus.NotApplicable, null, null);
            }

            var tokenCms = token.AsSignedCms();
            if (!token.VerifySignatureForHash(
                    HashData(hashAlgorithm, cms.Signature),
                    hashAlgorithm,
                    out var timestampCertificate,
                    tokenCms.Certificates) ||
                timestampCertificate is null)
            {
                return new TimestampValidation(PdfTrustStatus.Invalid, PdfTrustStatus.NotApplicable, token.TokenInfo.Timestamp, null);
            }

            using (timestampCertificate)
            {
                var enhancedKeyUsage = timestampCertificate.Extensions
                    .OfType<X509EnhancedKeyUsageExtension>()
                    .FirstOrDefault();
                if (enhancedKeyUsage is null ||
                    !enhancedKeyUsage.Critical ||
                    enhancedKeyUsage.EnhancedKeyUsages.Count != 1 ||
                    enhancedKeyUsage.EnhancedKeyUsages[0].Value != TimestampingExtendedKeyUsageOid)
                {
                    return new TimestampValidation(
                        PdfTrustStatus.Invalid,
                        PdfTrustStatus.NotApplicable,
                        token.TokenInfo.Timestamp,
                        timestampCertificate.Subject);
                }

                var certificateValidation = ValidateCertificate(
                    timestampCertificate,
                    tokenCms.Certificates.Cast<X509Certificate2>().ToArray(),
                    token.TokenInfo.Timestamp,
                    validationPolicy);
                var timestampStatus = certificateValidation.TrustStatus == PdfTrustStatus.Invalid ||
                                      certificateValidation.RevocationStatus == PdfTrustStatus.Invalid
                    ? PdfTrustStatus.Invalid
                    : certificateValidation.TrustStatus == PdfTrustStatus.Valid &&
                      certificateValidation.RevocationStatus is PdfTrustStatus.Valid or PdfTrustStatus.NotApplicable
                        ? PdfTrustStatus.Valid
                        : PdfTrustStatus.Indeterminate;
                return new TimestampValidation(
                    timestampStatus,
                    certificateValidation.RevocationStatus,
                    token.TokenInfo.Timestamp,
                    timestampCertificate.Subject);
            }
        }
        catch (CryptographicException)
        {
            return new TimestampValidation(PdfTrustStatus.Invalid, PdfTrustStatus.NotApplicable, null, null);
        }
    }

    private static bool TryGetHashAlgorithm(
        string oid,
        out HashAlgorithmName algorithm,
        out string displayName)
    {
        (algorithm, displayName) = oid switch
        {
            Sha256Oid => (HashAlgorithmName.SHA256, "SHA-256"),
            Sha384Oid => (HashAlgorithmName.SHA384, "SHA-384"),
            Sha512Oid => (HashAlgorithmName.SHA512, "SHA-512"),
            _ => (default, oid)
        };
        return algorithm != default;
    }

    private static byte[] HashData(HashAlgorithmName algorithm, ReadOnlySpan<byte> value) =>
        algorithm.Name switch
        {
            "SHA256" => SHA256.HashData(value),
            "SHA384" => SHA384.HashData(value),
            "SHA512" => SHA512.HashData(value),
            _ => throw new NotSupportedException($"Hash algorithm '{algorithm.Name}' is not supported.")
        };

    private static string? DescribePadesProfile(string? subFilter, ParsedCms cms) =>
        string.Equals(subFilter, "ETSI.CAdES.detached", StringComparison.Ordinal) &&
        cms.SigningCertificateV2Hash is not null
            ? cms.TimestampToken is null ? "B-B" : "B-T"
            : null;

    private static string ReadAlgorithmIdentifier(AsnReader algorithmIdentifier)
    {
        var oid = algorithmIdentifier.ReadObjectIdentifier();
        if (algorithmIdentifier.HasData)
        {
            algorithmIdentifier.ReadEncodedValue();
        }

        return oid;
    }

    private static bool TryTrimDer(ReadOnlySpan<byte> value, out ReadOnlySpan<byte> der)
    {
        der = default;
        if (value.Length < 2 || value[0] != 0x30)
        {
            return false;
        }

        int headerLength;
        int contentLength;
        if ((value[1] & 0x80) == 0)
        {
            headerLength = 2;
            contentLength = value[1];
        }
        else
        {
            var lengthBytes = value[1] & 0x7F;
            if (lengthBytes is < 1 or > 4 || value.Length < 2 + lengthBytes)
            {
                return false;
            }

            headerLength = 2 + lengthBytes;
            contentLength = 0;
            for (var index = 0; index < lengthBytes; index++)
            {
                contentLength = checked((contentLength << 8) | value[2 + index]);
            }
        }

        var totalLength = checked(headerLength + contentLength);
        if (totalLength > value.Length)
        {
            return false;
        }

        der = value[..totalLength];
        return true;
    }

    private static IReadOnlyList<long> ReadIntegerArray(PdfObject? value) =>
        value is PdfArray array
            ? array.Items.OfType<PdfInteger>().Select(static item => item.Value).ToArray()
            : [];

    private static string? ReadName(PdfObject? value) => value is PdfName name ? name.Value : null;

    private static PdfTrustDiagnostic Diagnostic(
        string code,
        PdfTrustDiagnosticSeverity severity,
        string message,
        PdfObjectId id) => new(code, severity, message, id.Number, id.Generation);

    private sealed record ParsedAttributes(
        byte[]? MessageDigest,
        byte[]? SigningCertificateV2Hash,
        DateTimeOffset? SigningTime);

    private sealed record ParsedCms(
        string DigestAlgorithmOid,
        string SignatureAlgorithmOid,
        byte[] SignedAttributes,
        byte[] Signature,
        byte[]? MessageDigest,
        byte[]? SigningCertificateV2Hash,
        DateTimeOffset? SigningTime,
        IReadOnlyList<X509Certificate2> Certificates,
        byte[]? TimestampToken);

    private sealed record CertificateValidation(
        PdfTrustStatus TrustStatus,
        PdfTrustStatus RevocationStatus);

    private sealed record TimestampValidation(
        PdfTrustStatus Status,
        PdfTrustStatus RevocationStatus,
        DateTimeOffset? Time,
        string? AuthoritySubject);
}
