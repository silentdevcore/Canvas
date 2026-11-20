using System.Security.Cryptography;

namespace PVAD.Licensing;

internal sealed class PvadLicenseVerifier
{
    private readonly Dictionary<string, string> publicKeys;

    internal PvadLicenseVerifier(IReadOnlyDictionary<string, string> publicKeys) =>
        this.publicKeys = new(publicKeys, StringComparer.Ordinal);

    internal PvadLicensePayload Verify(ReadOnlyMemory<byte> document)
    {
        var envelope = PvadLicenseJson.Read<PvadSignedNativeLicense>(document, PvadLicenseJson.MaximumEnvelopeBytes);
        if (envelope.SchemaVersion != 1 || envelope.Algorithm != PvadLicenseJson.Algorithm)
            throw PvadLicenseJson.Error("PVADLIC005", "The license schema or signature algorithm is unsupported.");
        var payload = VerifySignedPayload(envelope.KeyId, envelope.Payload, envelope.Signature);
        var license = PvadLicenseJson.Read<PvadLicensePayload>(payload, PvadLicenseJson.MaximumPayloadBytes);
        ValidatePayload(license);
        return license;
    }

    internal (PvadNativeRevocationList List, string PayloadSha256) VerifyRevocations(ReadOnlyMemory<byte> document)
    {
        var envelope = PvadLicenseJson.Read<PvadSignedNativeRevocationList>(document, PvadLicenseJson.MaximumEnvelopeBytes);
        if (envelope.SchemaVersion != 1 || envelope.Kind != PvadNativeRevocationList.DocumentKind || envelope.Algorithm != PvadLicenseJson.Algorithm)
            throw PvadLicenseJson.Error("PVADLIC005", "The native revocation schema or signature algorithm is unsupported.");
        var payload = VerifySignedPayload(envelope.KeyId, envelope.Payload, envelope.Signature);
        var list = PvadLicenseJson.Read<PvadNativeRevocationList>(payload, PvadLicenseJson.MaximumPayloadBytes);
        ValidateRevocations(list);
        return (list, Convert.ToHexStringLower(SHA256.HashData(payload)));
    }

    internal static void ValidateRevocations(PvadNativeRevocationList list)
    {
        if (list.SchemaVersion != 1 || list.Purpose != PvadNativeRevocationList.DocumentKind)
            throw PvadLicenseJson.Error("PVADLIC005", "The native revocation payload schema is unsupported.");
        if (list.Sequence < 1 || !IsUtc(list.IssuedAt) || !IsUtc(list.NextUpdate) || list.NextUpdate <= list.IssuedAt ||
            list.RevokedLicenseIds is null || list.RevokedLicenseIds.Length > 512 || list.RevokedLicenseIds.Contains(Guid.Empty) ||
            list.RevokedLicenseIds.Distinct().Count() != list.RevokedLicenseIds.Length)
            throw PvadLicenseJson.Error("PVADLIC004", "The native revocation metadata is malformed or exceeds its limits.");
    }

    private byte[] VerifySignedPayload(string keyId, string encodedPayload, string encodedSignature)
    {
        if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 100 ||
            !publicKeys.TryGetValue(keyId, out var publicKey))
            throw PvadLicenseJson.Error("PVADLIC006", "The license signing key is not trusted by this library.");
        if (encodedPayload is null || encodedPayload.Length > 44 * 1024 || encodedSignature is null || encodedSignature.Length != 88)
            throw PvadLicenseJson.Error("PVADLIC004", "The license payload or signature encoding is malformed.");

        byte[] payload;
        byte[] signature;
        try
        {
            payload = Convert.FromBase64String(encodedPayload);
            signature = Convert.FromBase64String(encodedSignature);
            if (signature.Length != 64)
                throw new FormatException();
        }
        catch (FormatException)
        {
            throw PvadLicenseJson.Error("PVADLIC004", "The license payload or signature encoding is malformed.");
        }

        if (payload.Length > PvadLicenseJson.MaximumPayloadBytes)
            throw PvadLicenseJson.Error("PVADLIC003", "The decoded license payload exceeds its size limit.");
        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(publicKey);
            if (verifier.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value ||
                !verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                throw PvadLicenseJson.Error("PVADLIC007", "The license signature is invalid.");
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            throw PvadLicenseJson.Error("PVADLIC007", "The license signature is invalid.");
        }

        return payload;
    }

    internal static void ValidatePayload(PvadLicensePayload license)
    {
        if (license.SchemaVersion != 1)
            throw PvadLicenseJson.Error("PVADLIC005", "The native license payload schema is unsupported.");
        if (license.LicenseId == Guid.Empty || string.IsNullOrWhiteSpace(license.CustomerReference) ||
            license.CustomerReference.Length > 160 || license.CustomerReference.Any(char.IsControl) ||
            !Enum.IsDefined(license.Model) || !IsUtc(license.IssuedAt) || !IsUtc(license.ValidFrom) ||
            license.ValidUntil is { } until && (!IsUtc(until) || until <= license.ValidFrom) ||
            license.UpdatesUntil is { } updates && (!IsUtc(updates) || updates < license.ValidFrom))
            throw PvadLicenseJson.Error("PVADLIC004", "The license identity or validity metadata is malformed.");

        if (license.Model == PvadLicenseModel.Perpetual
                ? license.ValidUntil is not null || license.UpdatesUntil is null
                : license.ValidUntil is null || license.UpdatesUntil is not null)
            throw PvadLicenseJson.Error("PVADLIC004", "The license term does not match its model.");
        if (license.Model == PvadLicenseModel.Trial &&
            (license.IssuedAt > license.ValidFrom || license.ValidFrom > DateTimeOffset.MaxValue.AddDays(-30) ||
             license.ValidUntil != license.ValidFrom.AddDays(30) || license.RedistributionAllowed))
            throw PvadLicenseJson.Error("PVADLIC004", "Trial licenses must cover exactly 30 days without redistribution rights.");
        if (license.Model == PvadLicenseModel.Subscription && license.ValidUntil > DateTimeOffset.MaxValue.AddDays(-7))
            throw PvadLicenseJson.Error("PVADLIC004", "The subscription term cannot represent its grace period.");
        if (license.Grants is null || license.Grants.Length is < 1 or > 64)
            throw PvadLicenseJson.Error("PVADLIC004", "The license must contain a bounded set of product grants.");

        var products = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grant in license.Grants)
        {
            if (grant is null || !PvadLicenseProducts.All.Contains(grant.Product, StringComparer.Ordinal) ||
                !products.Add(grant.Product) || grant.Providers is null || grant.Providers.Length > 128 ||
                grant.Providers.Distinct(StringComparer.Ordinal).Count() != grant.Providers.Length ||
                grant.Providers.Any(provider => !IsProvider(provider)) ||
                (PvadLicenseProducts.IsMigration(grant.Product) ? grant.Providers.Length == 0 : grant.Providers.Length != 0))
                throw PvadLicenseJson.Error("PVADLIC004", "The license product or provider grants are malformed.");
        }
    }

    private static bool IsProvider(string? provider) => provider is { Length: > 0 and <= 100 } && provider != "auto" &&
        char.IsAsciiLetterLower(provider[0]) &&
        provider.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');

    private static bool IsUtc(DateTimeOffset timestamp) => timestamp != default && timestamp.Offset == TimeSpan.Zero;
}
