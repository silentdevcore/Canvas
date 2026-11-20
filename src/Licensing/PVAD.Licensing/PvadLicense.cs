using System.Globalization;
using System.Reflection;

namespace PVAD.Licensing;

/// <summary>
/// Process-wide native license. Loading is explicit, offline, and atomic; it does not
/// authenticate users or replace server-side organization/entitlement checks.
/// </summary>
public static class PvadLicense
{
    private static readonly Lazy<PvadLicenseAuthority> Authority = new(CreateAuthority);

    /// <summary>Loads a bounded license file. Invalid replacements preserve the active license.</summary>
    public static PvadLicenseStatus LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using var stream = File.OpenRead(path);
            return LoadFromStream(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw PvadLicenseJson.Error("PVADLIC013", "The native license file could not be read.");
        }
    }

    /// <summary>Loads from the current stream position and leaves the caller's stream open.</summary>
    public static PvadLicenseStatus LoadFromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try { return Authority.Value.Load(stream); }
        catch (Exception exception) when (exception is IOException or NotSupportedException or ObjectDisposedException)
        {
            throw PvadLicenseJson.Error("PVADLIC013", "The native license stream could not be read.");
        }
    }

    public static PvadLicenseStatus GetStatus() => Authority.Value.GetStatus();

    /// <summary>Explicitly imports a signed cumulative offline list. Invalid replacements preserve all known revocations.</summary>
    public static PvadNativeRevocationStatus ImportRevocationsFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try { using var stream = File.OpenRead(path); return ImportRevocationsFromStream(stream); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw PvadLicenseJson.Error("PVADLIC013", "The native revocation file could not be read."); }
    }

    /// <summary>Imports from the current position, leaves the stream open, and never makes network requests.</summary>
    public static PvadNativeRevocationStatus ImportRevocationsFromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try { return Authority.Value.ImportRevocations(stream); }
        catch (Exception exception) when (exception is IOException or NotSupportedException or ObjectDisposedException)
        { throw PvadLicenseJson.Error("PVADLIC013", "The native revocation stream could not be read."); }
    }

    public static PvadNativeRevocationStatus GetRevocationStatus() => Authority.Value.GetRevocationStatus();

    /// <summary>Admits one operation against a single immutable license snapshot.</summary>
    public static PvadLicenseOperation Require(string product, string? provider = null) => Authority.Value.Require(product, provider);

    internal static byte[] CaptureImporterWorkerLicense() => Authority.Value.CaptureImporterWorkerLicense();
    internal static PvadNativeWorkerLicense CaptureImporterWorkerConfiguration() => Authority.Value.CaptureImporterWorkerConfiguration();
    internal static byte[]? CaptureRevocations() => Authority.Value.CaptureRevocations();

    internal static PvadLicenseExecution BeginOperation(PvadNativeOperation operation, string? provider = null) => Authority.Value.Begin(operation, provider);

    internal static PvadLicenseOperation DemandContinuation(string product) => PvadLicenseExecution.Demand(product, Authority.Value);

    internal static PvadLicenseOperation DemandMigrationContinuation(string product, string provider) =>
        PvadLicenseExecution.Demand(product, Authority.Value, provider);

    internal static PvadLicenseOperation DemandDependency(PvadNativeDependency dependency) => PvadLicenseExecution.DemandDependency(dependency, Authority.Value);

    internal static PvadLicenseExecution BeginNutrientDesignerPdfImport() =>
        PvadLicenseExecution.EnterNutrientDesignerPdfImport(Authority.Value);

    private static PvadLicenseAuthority CreateAuthority()
    {
        try
        {
            var assembly = typeof(PvadLicense).Assembly;
            var verifier = CreateEmbeddedVerifier();
            using var releaseStream = assembly.GetManifestResourceStream("PVAD.Licensing.LibraryRelease.json")!;
            var release = PvadLicenseJson.Read<LibraryRelease>(PvadLicenseJson.ReadBounded(releaseStream), PvadLicenseJson.MaximumEnvelopeBytes);
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
            if (release.Version != version || !DateOnly.TryParseExact(release.PublishedAt, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new InvalidOperationException();
            return new(verifier, TimeProvider.System, new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)));
        }
        catch (Exception exception) when (exception is PvadLicenseException or InvalidOperationException or ArgumentException or System.Security.Cryptography.CryptographicException)
        {
            throw PvadLicenseJson.Error("PVADLIC002", "The library's embedded license trust or release metadata is invalid.");
        }
    }

    internal static PvadLicenseVerifier CreateEmbeddedVerifier()
    {
        try
        {
            using var keysStream = typeof(PvadLicense).Assembly.GetManifestResourceStream("PVAD.Licensing.TrustedKeys.json")!;
            var store = PvadLicenseJson.Read<TrustStore>(PvadLicenseJson.ReadBounded(keysStream), PvadLicenseJson.MaximumEnvelopeBytes);
            if (store.SchemaVersion != 1 || store.Keys is null || store.Keys.Length > 32)
                throw new InvalidOperationException();
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in store.Keys)
            {
                if (key is null || string.IsNullOrWhiteSpace(key.KeyId) || key.KeyId.Length > 100 ||
                    string.IsNullOrWhiteSpace(key.PublicKeyPem) || key.PublicKeyPem.Contains("PRIVATE", StringComparison.OrdinalIgnoreCase) ||
                    !keys.TryAdd(key.KeyId, key.PublicKeyPem))
                    throw new InvalidOperationException();
                using var algorithm = System.Security.Cryptography.ECDsa.Create();
                algorithm.ImportFromPem(key.PublicKeyPem);
                if (algorithm.ExportParameters(false).Curve.Oid.Value != System.Security.Cryptography.ECCurve.NamedCurves.nistP256.Oid.Value)
                    throw new InvalidOperationException();
            }
            return new(keys);
        }
        catch (Exception exception) when (exception is PvadLicenseException or InvalidOperationException or ArgumentException or System.Security.Cryptography.CryptographicException)
        {
            throw PvadLicenseJson.Error("PVADLIC002", "The library's embedded license trust or release metadata is invalid.");
        }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record TrustStore(int SchemaVersion, TrustedKey[] Keys);
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record TrustedKey(string KeyId, string PublicKeyPem);
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record LibraryRelease(string Version, string PublishedAt);
}
