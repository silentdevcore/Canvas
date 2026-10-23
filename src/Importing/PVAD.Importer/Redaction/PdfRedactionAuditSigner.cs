using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Importer.Signing;

namespace PVAD.Importer.Redaction;

/// <summary>
/// Signs a redaction audit record with the dedicated audit key.
///
/// The signature is taken over the record's canonical hash rather than over the document, so
/// it attests to what was done and by whom without embedding anything about the removed
/// content. Signing is optional: without a key the record keeps its hash, which detects a
/// later change but cannot prove who produced it.
/// </summary>
internal static class PdfRedactionAuditSigner
{
    public static async Task<PdfRedactionAuditSignature?> SignAsync(
        string recordSha256,
        IPdfSigningKey? key,
        IPdfTimestampAuthority? timestampAuthority,
        CancellationToken cancellationToken)
    {
        if (key is null)
        {
            return null;
        }

        // CMS UTCTime has one-second precision. Store the same normalized value beside the
        // CMS so verification can compare the convenience metadata with the signed value.
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        // The CMS builder expects a SHA-256 digest; hashing the canonical hash string binds
        // the signature to exactly the value the record publishes.
        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(recordSha256));
        var cms = timestampAuthority is null
            ? PdfCadesCmsBuilder.CreateDetached(digest, key, signedAt)
            : await PdfCadesCmsBuilder.CreateDetachedWithTimestampAsync(
                digest,
                key,
                signedAt,
                timestampAuthority,
                cancellationToken).ConfigureAwait(false);
        var certificate = key.Certificate;
        return new PdfRedactionAuditSignature(
            "CMS/SHA-256",
            certificate.Subject,
            certificate.Thumbprint,
            signedAt,
            Convert.ToBase64String(cms.Cms),
            cms.TimestampToken?.Timestamp,
            cms.TimestampToken?.AuthoritySubject,
            cms.TimestampToken?.AuthorityThumbprint);
    }

    /// <summary>
    /// Recomputes the record hash from its canonical form. Used by verification and by any
    /// consumer that needs to confirm the signed value matches the record it was given.
    /// </summary>
    public static string ComputeRecordHash(
        string sourceSha256,
        string resultSha256,
        DateTimeOffset appliedAt,
        string? actor,
        string? reason,
        IReadOnlyList<PdfRedactionRegion> regions,
        IReadOnlyList<string> appliedSanitizations,
        bool verificationPassed)
    {
        using var canonical = new MemoryStream();
        using var writer = new Utf8JsonWriter(canonical, new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();
        writer.WriteString("sourceSha256", sourceSha256);
        writer.WriteString("resultSha256", resultSha256);
        writer.WriteString("appliedAt", appliedAt.ToUniversalTime());
        writer.WriteString("actor", actor);
        writer.WriteString("reason", reason);
        writer.WriteStartArray("sanitizations");
        foreach (var sanitization in appliedSanitizations)
        {
            writer.WriteStringValue(sanitization);
        }

        writer.WriteEndArray();
        writer.WriteBoolean("verificationPassed", verificationPassed);
        writer.WriteStartArray("regions");
        foreach (var region in regions)
        {
            writer.WriteStartObject();
            writer.WriteNumber("pageNumber", region.PageNumber);
            writer.WriteNumber("x", region.Bounds.X);
            writer.WriteNumber("y", region.Bounds.Y);
            writer.WriteNumber("width", region.Bounds.Width);
            writer.WriteNumber("height", region.Bounds.Height);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(canonical.ToArray()));
    }
}
