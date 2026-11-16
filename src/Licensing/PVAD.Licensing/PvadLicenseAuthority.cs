namespace PVAD.Licensing;

internal sealed class PvadLicenseAuthority(
    PvadLicenseVerifier verifier,
    TimeProvider timeProvider,
    DateTimeOffset libraryPublishedAt)
{
    private sealed record ActiveLicense(PvadLicensePayload Payload, byte[] Envelope);
    private ActiveLicense? current;

    internal PvadLicenseStatus Load(Stream stream)
    {
        var envelope = PvadLicenseJson.ReadBounded(stream);
        var candidate = verifier.Verify(envelope);
        var status = Status(candidate);
        if (!status.CanStartOperation)
            throw PvadLicenseJson.Error(status.Code, "The license is not valid for starting operations with this library.");
        Interlocked.Exchange(ref current, new ActiveLicense(candidate, envelope));
        return status;
    }

    internal PvadLicenseStatus GetStatus() => Status(Volatile.Read(ref current)?.Payload);

    internal PvadLicenseOperation Require(string product, string? provider)
        => Require(Volatile.Read(ref current)?.Payload, product, provider);

    internal PvadLicenseExecution Begin(PvadNativeOperation operation) =>
        PvadLicenseExecution.Enter(operation, Require(PvadLicenseExecution.ProductFor(operation), null), this);

    // The worker receives signed bytes from the same immutable snapshot being admitted.
    internal byte[] CaptureImporterWorkerLicense()
    {
        var snapshot = Volatile.Read(ref current);
        Require(snapshot?.Payload, PvadLicenseProducts.Importer, null);
        return snapshot!.Envelope.ToArray();
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
        var graceEnd = license.Model == PvadLicenseModel.Subscription ? license.ValidUntil?.AddDays(7) : null;
        var state = PvadLicenseState.Active;
        var code = "PVADLIC000";
        if (now < license.ValidFrom || now < license.IssuedAt)
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
