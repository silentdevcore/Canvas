using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PVAD.Licensing.Tests;

public sealed class NativeRevocationTests : IDisposable
{
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly Clock clock = new();
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private PvadLicenseVerifier Verifier() => new(new Dictionary<string, string> { ["test"] = key.ExportSubjectPublicKeyInfoPem() });
    private PvadLicenseAuthority Authority() => new(Verifier(), clock, Now.AddYears(-1));
    private static PvadLicensePayload License(Guid id) => new(1, id, "synthetic", PvadLicenseModel.Trial, Now, Now,
        Now.AddDays(30), null, false, [new("pdf", []), new("importer", [])]);
    private static PvadNativeRevocationList List(long sequence, params Guid[] ids) =>
        new(1, PvadNativeRevocationList.DocumentKind, sequence, Now, Now.AddDays(1), ids);
    private byte[] LicenseBytes(Guid id) => JsonSerializer.SerializeToUtf8Bytes(new PvadSignedNativeLicense(1, "test", PvadLicenseJson.Algorithm,
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(License(id), PvadLicenseJson.Options)),
        Signature(JsonSerializer.SerializeToUtf8Bytes(License(id), PvadLicenseJson.Options))), PvadLicenseJson.Options);
    private string Signature(byte[] payload) => Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    private byte[] Document(PvadNativeRevocationList list) => Envelope(JsonSerializer.SerializeToUtf8Bytes(list, PvadLicenseJson.Options));
    private byte[] Envelope(byte[] payload) => JsonSerializer.SerializeToUtf8Bytes(new PvadSignedNativeRevocationList(1,
        PvadNativeRevocationList.DocumentKind, "test", PvadLicenseJson.Algorithm, Convert.ToBase64String(payload), Signature(payload)), PvadLicenseJson.Options);
    private static PvadNativeRevocationStatus Import(PvadLicenseAuthority authority, byte[] bytes)
    { using var stream = new MemoryStream(bytes); return authority.ImportRevocations(stream); }
    private static PvadLicenseStatus Load(PvadLicenseAuthority authority, byte[] bytes)
    { using var stream = new MemoryStream(bytes); return authority.Load(stream); }

    [Fact]
    public void Import_before_activation_blocks_a_revoked_id_but_not_a_separately_issued_replacement()
    {
        var authority = Authority(); var id = Guid.NewGuid();
        Assert.False(authority.GetRevocationStatus().Loaded);
        var imported = Import(authority, Document(List(1, id)));
        Assert.True(imported.Loaded); Assert.Equal(1, imported.RevokedLicenseCount);
        Assert.False(imported.IsStale); Assert.Equal(PvadLicenseState.Unlicensed, authority.GetStatus().State);
        Assert.Equal("PVADLIC014", Assert.Throws<PvadLicenseException>(() => Load(authority, LicenseBytes(id))).Code);
        Assert.Equal(PvadLicenseState.Unlicensed, authority.GetStatus().State);
        var replacement = Guid.NewGuid(); Load(authority, LicenseBytes(replacement));
        Assert.Equal(replacement, authority.Require("pdf", null).LicenseId);
        Assert.Equal(imported, authority.GetRevocationStatus());
        Assert.Equal("PVADLIC014", Assert.Throws<PvadLicenseException>(() => Load(authority, LicenseBytes(id))).Code);
        Assert.Equal(replacement, authority.GetStatus().LicenseId);
    }

    [Fact]
    public void New_calls_fail_after_revocation_while_an_admitted_private_continuation_can_finish()
    {
        var authority = Authority(); var id = Guid.NewGuid(); Load(authority, LicenseBytes(id));
        using (authority.Begin(PvadNativeOperation.PdfEditing))
        {
            Import(authority, Document(List(1, id)));
            Assert.Equal(PvadLicenseState.Revoked, authority.GetStatus().State);
            Assert.False(authority.GetStatus().CanStartOperation);
            Assert.Equal(id, PvadLicenseExecution.Demand("importer", authority).LicenseId);
            Assert.Equal("PVADLIC014", Assert.Throws<PvadLicenseException>(() => authority.Require("pdf", null)).Code);
            Assert.Equal("PVADLIC014", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.PdfSave)).Code);
            Assert.Equal("PVADLIC014", Assert.Throws<PvadLicenseException>(() => authority.CaptureImporterWorkerConfiguration()).Code);
        }
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer", authority));
    }

    [Fact]
    public void Idempotent_signed_payload_replay_works_but_rollback_reuse_and_removed_ids_preserve_the_latest_list()
    {
        var authority = Authority(); var id = Guid.NewGuid(); var next = Guid.NewGuid();
        var current = List(2, id); var bytes = Document(current);
        var expected = Import(authority, bytes);
        Assert.Equal(expected, Import(authority, Document(current)));
        Assert.Equal(bytes, authority.CaptureRevocations());
        foreach (var rejected in new[] { List(1, id), List(2, id, next), List(3), List(3, next), List(3, id) with { IssuedAt = Now.AddSeconds(-1) }, List(3, id) with { IssuedAt = Now.AddSeconds(1) } })
        {
            Assert.Equal("PVADLIC015", Assert.Throws<PvadLicenseException>(() => Import(authority, Document(rejected))).Code);
            Assert.Equal(expected, authority.GetRevocationStatus());
        }
        Assert.Equal(3, Import(authority, Document(List(3, id, next))).Sequence);
        Assert.Equal(2, authority.GetRevocationStatus().RevokedLicenseCount);
    }

    [Fact]
    public void Stale_lists_keep_known_revocations_without_expiring_unrelated_valid_licenses()
    {
        var authority = Authority(); var revoked = Guid.NewGuid(); var healthy = Guid.NewGuid();
        clock.Now = Now.AddDays(2);
        Assert.True(Import(authority, Document(List(1, revoked))).IsStale);
        Load(authority, LicenseBytes(healthy)); Assert.True(authority.GetStatus().CanStartOperation);
        Assert.Equal("PVADLIC014", Assert.Throws<PvadLicenseException>(() => Load(authority, LicenseBytes(revoked))).Code);
        clock.Now = Now.AddSeconds(-1);
        Assert.True(authority.GetRevocationStatus().IsStale);
        clock.Now = Now.AddDays(3);
        Assert.True(authority.GetRevocationStatus().IsStale);
        Assert.Equal(1, authority.GetRevocationStatus().RevokedLicenseCount);
    }

    [Theory]
    [InlineData("schemaVersion", "2", "PVADLIC005")]
    [InlineData("kind", "\"license\"", "PVADLIC005")]
    [InlineData("algorithm", "\"none\"", "PVADLIC005")]
    [InlineData("keyId", "\"foreign\"", "PVADLIC006")]
    [InlineData("signature", "\"bad\"", "PVADLIC004")]
    [InlineData("payload", "\"%%%\"", "PVADLIC004")]
    [InlineData("extra", "true", "PVADLIC004")]
    public void Malformed_or_untrusted_envelopes_fail_without_changing_known_revocations(string property, string json, string code)
    {
        var authority = Authority(); var bytes = Document(List(1, Guid.NewGuid())); var expected = Import(authority, bytes);
        var changed = JsonNode.Parse(bytes)!.AsObject(); changed[property] = JsonNode.Parse(json);
        Assert.Equal(code, Assert.Throws<PvadLicenseException>(() => Import(authority, JsonSerializer.SerializeToUtf8Bytes(changed))).Code);
        Assert.Equal(expected, authority.GetRevocationStatus());
    }

    [Fact]
    public void Payload_schema_dates_ids_bounds_and_duplicate_properties_are_strict()
    {
        var good = List(1, Guid.NewGuid());
        foreach (var invalid in new[] { good with { Sequence = 0 }, good with { NextUpdate = Now }, good with { IssuedAt = default },
            good with { NextUpdate = Now.ToOffset(TimeSpan.FromHours(1)) }, good with { RevokedLicenseIds = null! },
            good with { RevokedLicenseIds = [Guid.Empty] }, good with { RevokedLicenseIds = [good.RevokedLicenseIds[0], good.RevokedLicenseIds[0]] },
            good with { RevokedLicenseIds = Enumerable.Range(0, 513).Select(_ => Guid.NewGuid()).ToArray() } })
            Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().VerifyRevocations(Document(invalid))).Code);
        Assert.Equal("PVADLIC005", Assert.Throws<PvadLicenseException>(() => Verifier().VerifyRevocations(Document(good with { Purpose = "license" }))).Code);
        var json = JsonSerializer.Serialize(good, PvadLicenseJson.Options);
        Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().VerifyRevocations(Envelope(
            Encoding.UTF8.GetBytes(json.Replace("\"sequence\":1", "\"sequence\":1,\"Sequence\":1"))))).Code);
        Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().VerifyRevocations(Envelope([0xff, 0xfe]))).Code);
        Assert.Equal("PVADLIC003", Assert.Throws<PvadLicenseException>(() => Verifier().VerifyRevocations(new byte[65537])).Code);
        Assert.Equal("PVADLIC003", Assert.Throws<PvadLicenseException>(() => Verifier().VerifyRevocations(Envelope(new byte[32769]))).Code);
    }

    [Fact]
    public void Signatures_and_document_purpose_cannot_be_substituted_for_a_license()
    {
        var bytes = Document(List(1, Guid.NewGuid()));
        var envelope = JsonNode.Parse(bytes)!;
        var payload = Convert.FromBase64String(envelope["payload"]!.GetValue<string>()); payload[0] ^= 1;
        envelope["payload"] = Convert.ToBase64String(payload);
        Assert.Equal("PVADLIC007", Assert.Throws<PvadLicenseException>(() => Verifier().VerifyRevocations(JsonSerializer.SerializeToUtf8Bytes(envelope))).Code);
        Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(bytes)).Code);
        Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().VerifyRevocations(LicenseBytes(Guid.NewGuid()))).Code);
        Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().VerifyRevocations(Envelope(JsonSerializer.SerializeToUtf8Bytes(License(Guid.NewGuid()), PvadLicenseJson.Options)))).Code);
    }

    [Fact]
    public void Known_signer_rotation_does_not_reset_the_global_sequence()
    {
        using var nextKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = new PvadLicenseVerifier(new Dictionary<string, string> { ["test"] = key.ExportSubjectPublicKeyInfoPem(), ["next"] = nextKey.ExportSubjectPublicKeyInfoPem() });
        var authority = new PvadLicenseAuthority(verifier, clock, Now.AddYears(-1));
        var id = Guid.NewGuid(); Import(authority, Document(List(3, id)));
        byte[] Next(long sequence)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(List(sequence, id), PvadLicenseJson.Options);
            return JsonSerializer.SerializeToUtf8Bytes(new PvadSignedNativeRevocationList(1, PvadNativeRevocationList.DocumentKind, "next", PvadLicenseJson.Algorithm,
                Convert.ToBase64String(payload), Convert.ToBase64String(nextKey.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))), PvadLicenseJson.Options);
        }
        Assert.Equal("PVADLIC015", Assert.Throws<PvadLicenseException>(() => Import(authority, Next(1))).Code);
        Assert.Equal(4, Import(authority, Next(4)).Sequence);
    }

    [Fact]
    public void Stream_position_and_independent_worker_buffers_are_preserved()
    {
        var authority = Authority(); Load(authority, LicenseBytes(Guid.NewGuid()));
        var bytes = Document(List(1, Guid.NewGuid()));
        using var stream = new MemoryStream([1, 2, .. bytes]); stream.Position = 2;
        authority.ImportRevocations(stream); Assert.True(stream.CanRead); Assert.Equal(stream.Length, stream.Position);
        var first = authority.CaptureImporterWorkerConfiguration(); var second = authority.CaptureImporterWorkerConfiguration();
        Assert.NotSame(first.License, second.License); Assert.NotSame(first.Revocations, second.Revocations);
        Assert.Equal(bytes, first.Revocations);
        first.Revocations![0] ^= 1; first.License[0] ^= 1;
        Assert.Equal(second.Revocations, authority.CaptureRevocations());
        Assert.Equal(second.License, authority.CaptureImporterWorkerLicense());
        using var large = new MemoryStream(new byte[65538]);
        Assert.Equal("PVADLIC003", Assert.Throws<PvadLicenseException>(() => authority.ImportRevocations(large)).Code);
        Assert.Equal(65537, large.Position); Assert.True(large.CanRead);
    }

    [Fact]
    public async Task Concurrent_license_load_and_revocation_import_never_restore_revoked_rights()
    {
        var authority = Authority(); var id = Guid.NewGuid(); var license = LicenseBytes(id); Load(authority, license);
        var lists = Enumerable.Range(1, 32).Select(index => Document(List(index, id))).ToArray();
        await Task.WhenAll(lists.Select(bytes => Task.Run(() =>
        {
            try { Import(authority, bytes); } catch (PvadLicenseException error) { Assert.Equal("PVADLIC015", error.Code); }
            for (var attempt = 0; attempt < 8; attempt++)
                Assert.Equal("PVADLIC014", Assert.Throws<PvadLicenseException>(() => Load(authority, license)).Code);
        })));
        Assert.Equal(32, authority.GetRevocationStatus().Sequence);
        Assert.Equal("PVADLIC014", authority.GetStatus().Code);
    }

    [Fact]
    public void Public_IO_errors_are_sanitized_and_do_not_change_process_status()
    {
        var before = PvadLicense.GetRevocationStatus();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");
        var error = Assert.Throws<PvadLicenseException>(() => PvadLicense.ImportRevocationsFromFile(missing));
        Assert.Equal("PVADLIC013", error.Code); Assert.DoesNotContain(missing, error.Message);
        using var stream = new MemoryStream(Document(List(1))); stream.Dispose();
        Assert.Equal("PVADLIC013", Assert.Throws<PvadLicenseException>(() => PvadLicense.ImportRevocationsFromStream(stream)).Code);
        Assert.Throws<ArgumentNullException>(() => PvadLicense.ImportRevocationsFromStream(null!));
        Assert.Equal(before, PvadLicense.GetRevocationStatus());
    }

    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = NativeRevocationTests.Now; public override DateTimeOffset GetUtcNow() => Now; }
    public void Dispose() => key.Dispose();
}
