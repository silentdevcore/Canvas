using System.Security.Cryptography;
using System.Text.Json;
using PVAD.Licensing;

namespace PVAD.Licensing.Tests;

public sealed class StrictExpiryTests
{
    private static readonly DateTimeOffset Start = new(2027, 8, 31, 12, 0, 0, TimeSpan.Zero);
    private static PvadLicensePayload Payload() => new(2, Guid.NewGuid(), "synthetic-expiry", PvadLicenseModel.Subscription,
        Start, Start, Start.AddMonths(3), null, false, [new(PvadLicenseProducts.Pdf, [])])
        { AccountScope = new("individualDeveloper", ["export.pdf"]), GracePeriodDays = 0 };
    private static byte[] Sign(PvadLicensePayload payload, ECDsa key) => JsonSerializer.SerializeToUtf8Bytes(new PvadSignedNativeLicense(
        payload.SchemaVersion, "synthetic-expiry", PvadLicenseJson.Algorithm,
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload, PvadLicenseJson.Options)),
        Convert.ToBase64String(key.SignData(JsonSerializer.SerializeToUtf8Bytes(payload, PvadLicenseJson.Options), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation))), PvadLicenseJson.Options);

    [Fact]
    public void Signed_zero_grace_blocks_exactly_at_expiry_and_retains_legacy_grace()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = new PvadLicenseVerifier(new Dictionary<string, string> { ["synthetic-expiry"] = key.ExportSubjectPublicKeyInfoPem() });
        var clock = new Clock { Now = Start };
        var authority = new PvadLicenseAuthority(verifier, clock, Start.AddYears(-1));
        var payload = Payload();
        authority.Load(new MemoryStream(Sign(payload, key)));
        clock.Now = payload.ValidUntil!.Value.AddTicks(-1);
        authority.RequireCapability(PvadLicenseProducts.Pdf, "export.pdf");
        clock.Now = payload.ValidUntil.Value;
        Assert.Equal(PvadLicenseState.Expired, authority.GetStatus().State);
        Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.RequireCapability(PvadLicenseProducts.Pdf, "export.pdf")).Code);
        authority.Load(new MemoryStream(Sign(payload with { GracePeriodDays = null }, key)));
        Assert.Equal(PvadLicenseState.GracePeriod, authority.GetStatus().State);
        Assert.True(authority.GetStatus().CanStartOperation);
        authority.Load(new MemoryStream(Sign(payload with { SchemaVersion = 1, AccountScope = null, GracePeriodDays = null }, key)));
        Assert.Equal(PvadLicenseState.GracePeriod, authority.GetStatus().State);
        clock.Now = payload.ValidUntil.Value.AddDays(7);
        Assert.Equal(PvadLicenseState.Expired, authority.GetStatus().State);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(8)]
    public void Unknown_signed_grace_values_are_rejected(int days) =>
        Assert.Throws<PvadLicenseException>(() => PvadLicenseVerifier.ValidatePayload(Payload() with { GracePeriodDays = days }));

    [Fact]
    public void Grace_override_requires_scoped_subscription_format()
    {
        Assert.Throws<PvadLicenseException>(() => PvadLicenseVerifier.ValidatePayload(Payload() with { SchemaVersion = 1, AccountScope = null }));
        Assert.Throws<PvadLicenseException>(() => PvadLicenseVerifier.ValidatePayload(Payload() with { Model = PvadLicenseModel.Trial, ValidUntil = Start.AddDays(30) }));
    }

    [Fact]
    public void Removing_or_extending_signed_grace_invalidates_the_signature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = new PvadLicenseVerifier(new Dictionary<string, string> { ["synthetic-expiry"] = key.ExportSubjectPublicKeyInfoPem() });
        var payload = Payload();
        var envelope = JsonSerializer.Deserialize<PvadSignedNativeLicense>(Sign(payload, key), PvadLicenseJson.Options)!;
        foreach (var replacement in new int?[] { null, 7 })
        {
            var changed = envelope with { Payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload with { GracePeriodDays = replacement }, PvadLicenseJson.Options)) };
            Assert.Throws<PvadLicenseException>(() => verifier.Verify(JsonSerializer.SerializeToUtf8Bytes(changed, PvadLicenseJson.Options)));
        }
    }

    private sealed class Clock : TimeProvider { internal DateTimeOffset Now { get; set; } public override DateTimeOffset GetUtcNow() => Now; }
}
