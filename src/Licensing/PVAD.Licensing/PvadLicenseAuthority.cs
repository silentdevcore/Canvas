namespace PVAD.Licensing;

internal sealed class PvadLicenseAuthority(
    PvadLicenseVerifier verifier,
    TimeProvider timeProvider,
    DateTimeOffset libraryPublishedAt)
{
    private sealed record ActiveLicense(PvadLicensePayload Payload, byte[] Envelope);
    private sealed record ActiveRevocations(PvadNativeRevocationList List, HashSet<Guid> Revoked, byte[] Envelope, string Hash);
    private readonly object gate = new();
    private ActiveLicense? current;
    private ActiveRevocations? revocations;

    internal PvadLicenseStatus Load(Stream stream)
    {
        var envelope = PvadLicenseJson.ReadBounded(stream);
        var candidate = verifier.Verify(envelope);
        lock (gate)
        {
            var status = Status(candidate);
            if (!status.CanStartOperation)
                throw PvadLicenseJson.Error(status.Code, "The license is not valid for starting operations with this library.");
            current = new ActiveLicense(candidate, envelope);
            return status;
        }
    }

    internal PvadLicenseStatus GetStatus() { lock (gate) return Status(current?.Payload); }

    internal PvadNativeRevocationStatus ImportRevocations(Stream stream)
    {
        var envelope = PvadLicenseJson.ReadBounded(stream);
        var (candidate, hash) = verifier.VerifyRevocations(envelope);
        var ids = candidate.RevokedLicenseIds.ToHashSet();
        lock (gate)
        {
            if (candidate.IssuedAt > timeProvider.GetUtcNow() || revocations is { } previous &&
                (candidate.Sequence < previous.List.Sequence ||
                 candidate.Sequence == previous.List.Sequence && hash != previous.Hash ||
                 candidate.IssuedAt < previous.List.IssuedAt || !previous.Revoked.IsSubsetOf(ids)))
                throw PvadLicenseJson.Error("PVADLIC015", "The revocation list is future-dated, rolls back history, or removes known revocations.");
            if (revocations?.List.Sequence != candidate.Sequence)
                revocations = new(candidate, ids, envelope, hash);
            return RevocationStatus();
        }
    }

    internal PvadNativeRevocationStatus GetRevocationStatus() { lock (gate) return RevocationStatus(); }
    internal byte[]? CaptureRevocations() { lock (gate) return revocations?.Envelope.ToArray(); }

    private PvadNativeRevocationStatus RevocationStatus() => revocations is null ? new(false) :
        new(true, revocations.List.Sequence, revocations.List.IssuedAt, revocations.List.NextUpdate,
            revocations.Revoked.Count, timeProvider.GetUtcNow() >= revocations.List.NextUpdate || timeProvider.GetUtcNow() < revocations.List.IssuedAt,
            revocations.Hash);

    internal PvadLicenseOperation Require(string product, string? provider)
    {
        lock (gate)
        {
            var admitted = Require(current?.Payload, product, provider);
            if (PvadLicenseProducts.IsMigration(product)) CheckCapability(current!.Payload, product);
            return admitted;
        }
    }

    internal PvadLicenseExecution Begin(PvadNativeOperation operation, string? provider = null, string? capability = null,
        string? additionalCapability = null)
    {
        lock (gate)
        {
            var admitted = Require(current?.Payload, PvadLicenseExecution.ProductFor(operation), provider);
            CheckCapability(current!.Payload, capability ?? PvadLicenseExecution.CapabilityFor(operation));
            if (additionalCapability is not null) CheckCapability(current.Payload, additionalCapability);
            return PvadLicenseExecution.Enter(operation, admitted, this);
        }
    }

    internal PvadLicenseOperation RequireCapability(string product, string capability)
    {
        lock (gate)
        {
            var admitted = Require(current?.Payload, product, null);
            CheckCapability(current!.Payload, capability);
            return admitted;
        }
    }

    internal void RequireAccountCapability(string capability)
    {
        lock (gate)
        {
            var status = Status(current?.Payload);
            if (current is null || !status.CanStartOperation)
                throw PvadLicenseJson.Error(status.Code, "A valid license is required for this capability.");
            CheckCapability(current.Payload, capability);
        }
    }

    private static void CheckCapability(PvadLicensePayload license, string? capability)
    {
        if (capability is null)
        {
            if (license.AccountScope is not null)
                throw PvadLicenseJson.Error("PVADLIC016", "A scoped license requires an explicit capability.");
            return;
        }
        PvadAccountCapabilityContext.Require(capability);
        if (license.AccountScope is { } scope && !scope.Capabilities.Contains(capability, StringComparer.Ordinal))
            throw PvadLicenseJson.Error("PVADLIC016", "The license does not include the requested capability.");
    }

    // The worker receives signed bytes from the same immutable snapshot being admitted.
    internal byte[] CaptureImporterWorkerLicense()
    {
        return CaptureImporterWorkerConfiguration().License;
    }

    internal PvadNativeWorkerLicense CaptureImporterWorkerConfiguration()
    {
        lock (gate)
        {
            Require(current?.Payload, PvadLicenseProducts.Importer, null);
            CheckCapability(current!.Payload, "image.ocr");
            return new(current!.Envelope.ToArray(), revocations?.Envelope.ToArray());
        }
    }

    private PvadLicenseOperation Require(PvadLicensePayload? snapshot, string product, string? provider)
    {
        if (!PvadLicenseProducts.All.Contains(product, StringComparer.Ordinal) ||
            (PvadLicenseProducts.IsMigration(product) ? string.IsNullOrWhiteSpace(provider) : provider is not null))
            throw new ArgumentException("Use a known native product and an explicit provider for migrations.", nameof(product));
        var status = Status(snapshot);
        if (snapshot is null || !status.CanStartOperation)
            throw PvadLicenseJson.Error(status.Code, "A valid native license is required to start this operation.");
        var grant = snapshot.Grants.FirstOrDefault(value => value.Product == product);
        if (grant is null || provider is not null && !grant.Providers.Contains(provider, StringComparer.Ordinal))
            throw PvadLicenseJson.Error("PVADLIC012", "The license does not include the requested product or provider.");
        return new(snapshot.LicenseId, product, provider, status.State == PvadLicenseState.GracePeriod);
    }

    private PvadLicenseStatus Status(PvadLicensePayload? license)
    {
        if (license is null)
            return new(PvadLicenseState.Unlicensed, "PVADLIC001");
        var now = timeProvider.GetUtcNow();
        var graceDays = license.GracePeriodDays ?? 7;
        var graceEnd = license.Model == PvadLicenseModel.Subscription && graceDays > 0 ? license.ValidUntil?.AddDays(graceDays) : null;
        var state = PvadLicenseState.Active;
        var code = "PVADLIC000";
        if (revocations?.Revoked.Contains(license.LicenseId) == true)
            (state, code) = (PvadLicenseState.Revoked, "PVADLIC014");
        else if (now < license.ValidFrom || now < license.IssuedAt)
            (state, code) = (PvadLicenseState.NotYetValid, "PVADLIC008");
        else if (license.Model == PvadLicenseModel.Perpetual && libraryPublishedAt > license.UpdatesUntil)
            (state, code) = (PvadLicenseState.UpdateNotCovered, "PVADLIC011");
        else if (license.ValidUntil is { } expires && now >= expires)
            (state, code) = graceEnd is { } grace && now < grace
                ? (PvadLicenseState.GracePeriod, "PVADLIC010")
                : (PvadLicenseState.Expired, "PVADLIC009");
        return new(state, code, license.LicenseId, license.Model, license.ValidUntil, graceEnd, license.UpdatesUntil);
    }
}
