using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;

namespace PXA.Importer.Redaction;

/// <summary>Verifies the canonical record hash and its optional detached CMS signature.</summary>
public static class PdfRedactionAuditVerifier
{
    private const string SigningTimeOid = "1.2.840.113549.1.9.5";

    public static bool Verify(PdfRedactionAuditRecord record, bool requireSignature = false)
    {
        ArgumentNullException.ThrowIfNull(record);

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
            return !requireSignature;
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

            var signingTime = cms.SignerInfos[0].SignedAttributes
                .Cast<CryptographicAttributeObject>()
                .FirstOrDefault(attribute => attribute.Oid?.Value == SigningTimeOid)?
                .Values
                .Cast<AsnEncodedData>()
                .Select(static value => new Pkcs9SigningTime(value.RawData).SigningTime)
                .FirstOrDefault();
            return signingTime.HasValue &&
                new DateTimeOffset(signingTime.Value.ToUniversalTime()) == record.Signature.SignedAt;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
