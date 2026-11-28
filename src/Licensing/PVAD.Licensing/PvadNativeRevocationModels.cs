namespace PVAD.Licensing;

/// <summary>Cumulative, manufacturer-signed offline revocations. Timestamps are UTC.</summary>
public sealed record PvadNativeRevocationList(int SchemaVersion, string Purpose, long Sequence,
    DateTimeOffset IssuedAt, DateTimeOffset NextUpdate, Guid[] RevokedLicenseIds)
{
    public const string DocumentKind = "PVAD.NativeLicenseRevocations";
}

/// <summary>The signature covers the exact decoded payload bytes, using embedded manufacturer trust.</summary>
public sealed record PvadSignedNativeRevocationList(int SchemaVersion, string Kind, string KeyId,
    string Algorithm, string Payload, string Signature);

/// <summary>Content-free process-local revocation metadata. Stale lists retain every known revocation.</summary>
public sealed record PvadNativeRevocationStatus(bool Loaded, long? Sequence = null, DateTimeOffset? IssuedAt = null,
    DateTimeOffset? NextUpdate = null, int RevokedLicenseCount = 0, bool IsStale = false, string? PayloadSha256 = null);

internal sealed record PvadNativeWorkerLicense(byte[] License, byte[]? Revocations);
