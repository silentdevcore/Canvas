using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PXA.Importer.Redaction;

/// <summary>Verifies the canonical record hash and its optional detached CMS signature.</summary>
public static class PdfRedactionAuditVerifier
{
    private const string SigningTimeOid = "1.2.840.113549.1.9.5";
    private const string SignatureTimestampTokenOid = "1.2.840.113549.1.9.16.2.14";
    private const string TimestampingExtendedKeyUsageOid = "1.3.6.1.5.5.7.3.8";

    public static bool Verify(
        PdfRedactionAuditRecord record,
        bool requireSignature = false,
        bool requireTimestamp = false)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!IsSha256(record.SourceSha256) ||
            !IsSha256(record.ResultSha256) ||
            !IsSha256(record.RecordSha256) ||
            record.Regions is null ||
            record.AppliedSanitizations is null ||
            record.Regions.Any(static region =>
                region.PageNumber < 1 ||
                !double.IsFinite(region.Bounds.X) ||
                !double.IsFinite(region.Bounds.Y) ||
                !double.IsFinite(region.Bounds.Width) ||
                !double.IsFinite(region.Bounds.Height)) ||
            record.AppliedSanitizations.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        var expectedHash = PdfRedactionAuditSigner.ComputeRecordHash(
            record.SourceSha256,
            record.ResultSha256,
            record.AppliedAt,
            record.Actor,
            record.Reason,
            record.Regions,
            record.AppliedSanitizations,
            record.VerificationPassed);
        if (!string.Equals(expectedHash, record.RecordSha256, StringComparison.Ordinal))
        {
            return false;
        }

        if (record.Signature is null)
        {
            return !requireSignature && !requireTimestamp;
        }

        try
        {
            if (!string.Equals(record.Signature.Algorithm, "CMS/SHA-256", StringComparison.Ordinal))
            {
                return false;
            }

            var content = new ContentInfo(Encoding.ASCII.GetBytes(record.RecordSha256));
            var cms = new SignedCms(content, detached: true);
            cms.Decode(Convert.FromBase64String(record.Signature.Cms));
            cms.CheckSignature(verifySignatureOnly: true);
            if (cms.SignerInfos.Count != 1 || cms.SignerInfos[0].Certificate is not { } certificate)
            {
                return false;
            }

            if (!string.Equals(certificate.Thumbprint, record.Signature.SignerThumbprint, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(certificate.Subject, record.Signature.SignerSubject, StringComparison.Ordinal))
            {
                return false;
            }

            var signer = cms.SignerInfos[0];
            var signingTime = signer.SignedAttributes
                .Cast<CryptographicAttributeObject>()
                .FirstOrDefault(attribute => attribute.Oid?.Value == SigningTimeOid)?
                .Values
                .Cast<AsnEncodedData>()
                .Select(static value => new Pkcs9SigningTime(value.RawData).SigningTime)
                .FirstOrDefault();
            if (!signingTime.HasValue ||
                new DateTimeOffset(signingTime.Value.ToUniversalTime()) != record.Signature.SignedAt)
            {
                return false;
            }

            return VerifyTimestamp(record.Signature, signer, certificate, requireTimestamp);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool VerifyTimestamp(
        PdfRedactionAuditSignature signature,
        SignerInfo signer,
        X509Certificate2 signingCertificate,
        bool requireTimestamp)
    {
        var timestampAttributes = signer.UnsignedAttributes
            .Cast<CryptographicAttributeObject>()
            .Where(attribute => attribute.Oid?.Value == SignatureTimestampTokenOid)
            .ToArray();
        if (timestampAttributes.Length == 0)
        {
            return !requireTimestamp &&
                signature.TimestampTime is null &&
                signature.TimestampAuthoritySubject is null &&
                signature.TimestampAuthorityThumbprint is null;
        }
        if (timestampAttributes.Length != 1 ||
            timestampAttributes[0].Values.Count != 1 ||
            signature.TimestampTime is null ||
            string.IsNullOrWhiteSpace(signature.TimestampAuthoritySubject) ||
            string.IsNullOrWhiteSpace(signature.TimestampAuthorityThumbprint))
        {
            return false;
        }

        var encodedToken = timestampAttributes[0].Values[0].RawData;
        if (!Rfc3161TimestampToken.TryDecode(encodedToken, out var token, out var consumed) ||
            token is null || consumed != encodedToken.Length)
        {
            return false;
        }

        var tokenCms = token.AsSignedCms();
        if (!token.VerifySignatureForHash(
                SHA256.HashData(signer.GetSignature()),
                HashAlgorithmName.SHA256,
                out var timestampCertificate,
                tokenCms.Certificates) ||
            timestampCertificate is null)
        {
            return false;
        }

        using (timestampCertificate)
        {
            var eku = timestampCertificate.Extensions
                .OfType<X509EnhancedKeyUsageExtension>()
                .SingleOrDefault();
            return eku is not null &&
                eku.Critical &&
                eku.EnhancedKeyUsages.Count == 1 &&
                eku.EnhancedKeyUsages[0].Value == TimestampingExtendedKeyUsageOid &&
                token.TokenInfo.Timestamp == signature.TimestampTime &&
                IsCertificateValidAt(signingCertificate, token.TokenInfo.Timestamp) &&
                IsCertificateValidAt(timestampCertificate, token.TokenInfo.Timestamp) &&
                string.Equals(
                    timestampCertificate.Subject,
                    signature.TimestampAuthoritySubject,
                    StringComparison.Ordinal) &&
                string.Equals(
                    timestampCertificate.Thumbprint,
                    signature.TimestampAuthorityThumbprint,
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsCertificateValidAt(X509Certificate2 certificate, DateTimeOffset timestamp) =>
        timestamp >= new DateTimeOffset(certificate.NotBefore.ToUniversalTime()) &&
        timestamp <= new DateTimeOffset(certificate.NotAfter.ToUniversalTime());

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
