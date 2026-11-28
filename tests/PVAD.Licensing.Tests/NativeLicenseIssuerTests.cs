using System.Security.Cryptography;
using System.Text.Json;
using PVAD.LicenseIssuer;

namespace PVAD.Licensing.Tests;

public sealed class NativeLicenseIssuerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "pvad-issuer-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public NativeLicenseIssuerTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void IssueIsVerifiableOfflineAndDoesNotLogPayloadOrKey()
    {
        var args = Prepare();
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, NativeLicenseIssuer.Run(args, output, error));
        var bytes = File.ReadAllBytes(Path.Combine(directory, "license.json"));
        var verifier = new PvadLicenseVerifier(new Dictionary<string, string> { ["synthetic"] = key.ExportSubjectPublicKeyInfoPem() });
        var payload = verifier.Verify(bytes);
        Assert.Equal("synthetic-customer-do-not-log", payload.CustomerReference);
        Assert.Equal(string.Empty, error.ToString());
        Assert.DoesNotContain(payload.CustomerReference, output.ToString());
        Assert.DoesNotContain("PRIVATE KEY", output.ToString());
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(directory, "license.json")));
    }

    [Fact]
    public void IssuerCannotOverwriteExistingArtifacts()
    {
        var args = Prepare();
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, NativeLicenseIssuer.Run(args, output, error));
        var path = Path.Combine(directory, "license.json");
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(1, NativeLicenseIssuer.Run(args, output, error));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("--bad")]
    [InlineData("--output")]
    public void UnknownOrDuplicateArgumentsAreRejected(string replacement)
    {
        var args = Prepare();
        args[0] = replacement;
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(1, NativeLicenseIssuer.Run(args, output, error));
        Assert.False(File.Exists(Path.Combine(directory, "license.json")));
    }

    [Fact]
    public void MissingArgumentsAndUnreadableKeyFailWithoutAutomaticKeyCreation()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(1, NativeLicenseIssuer.Run([], output, error));
        var args = Prepare();
        File.Delete(Path.Combine(directory, "key.pem"));
        Assert.Equal(1, NativeLicenseIssuer.Run(args, output, error));
        Assert.False(File.Exists(Path.Combine(directory, "key.pem")));
        Assert.False(File.Exists(Path.Combine(directory, "license.json")));
    }

    [Fact]
    public void PublicOnlyWrongCurveAndInsecurePrivateKeyFilesAreRejected()
    {
        var args = Prepare();
        using var output = new StringWriter();
        using var error = new StringWriter();
        WritePrivateKey(key.ExportSubjectPublicKeyInfoPem());
        Assert.Equal(1, NativeLicenseIssuer.Run(args, output, error));
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        WritePrivateKey(other.ExportECPrivateKeyPem());
        Assert.Equal(1, NativeLicenseIssuer.Run(args, output, error));
        if (!OperatingSystem.IsWindows())
        {
            WritePrivateKey(key.ExportECPrivateKeyPem());
            File.SetUnixFileMode(Path.Combine(directory, "key.pem"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            Assert.Equal(1, NativeLicenseIssuer.Run(args, output, error));
        }
        Assert.False(File.Exists(Path.Combine(directory, "license.json")));
    }

    [Fact]
    public void InvalidPayloadIsNotIssued()
    {
        var args = Prepare();
        File.WriteAllText(Path.Combine(directory, "payload.json"), "{}");
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(1, NativeLicenseIssuer.Run(args, output, error));
        Assert.Contains("PVADLIC004", error.ToString());
        Assert.False(File.Exists(Path.Combine(directory, "license.json")));
    }

    [Fact]
    public void Revocation_preparation_signs_exact_bytes_without_rewriting_existing_artifacts_or_logging_ids()
    {
        var args = Prepare();
        var now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var id = Guid.NewGuid();
        var payload = new PvadNativeRevocationList(1, PvadNativeRevocationList.DocumentKind, 3, now, now.AddDays(7), [id]);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(PvadLicenseJson.Options) { WriteIndented = true });
        File.WriteAllBytes(Path.Combine(directory, "payload.json"), bytes);
        using var output = new StringWriter(); using var error = new StringWriter();
        var command = new[] { "revocations" }.Concat(args).ToArray();
        Assert.Equal(0, NativeLicenseIssuer.Run(command, output, error));
        var artifact = File.ReadAllBytes(Path.Combine(directory, "license.json"));
        var verifier = new PvadLicenseVerifier(new Dictionary<string, string> { ["synthetic"] = key.ExportSubjectPublicKeyInfoPem() });
        var verified = verifier.VerifyRevocations(artifact);
        Assert.Equal(id, Assert.Single(verified.List.RevokedLicenseIds));
        var envelope = JsonSerializer.Deserialize<PvadSignedNativeRevocationList>(artifact, PvadLicenseJson.Options)!;
        Assert.Equal(bytes, Convert.FromBase64String(envelope.Payload));
        Assert.DoesNotContain(id.ToString(), output.ToString()); Assert.Empty(error.ToString());
        Assert.Equal(1, NativeLicenseIssuer.Run(command, output, error));
        Assert.Equal(artifact, File.ReadAllBytes(Path.Combine(directory, "license.json")));
    }

    [Fact]
    public void Revocation_preparation_rejects_license_payloads_and_malformed_lists_before_writing_output()
    {
        var args = Prepare(); using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(1, NativeLicenseIssuer.Run(new[] { "revocations" }.Concat(args).ToArray(), output, error));
        Assert.False(File.Exists(Path.Combine(directory, "license.json")));
        File.WriteAllText(Path.Combine(directory, "payload.json"), "{}");
        Assert.Equal(1, NativeLicenseIssuer.Run(new[] { "revocations" }.Concat(args).ToArray(), output, error));
        Assert.False(File.Exists(Path.Combine(directory, "license.json")));
    }

    private string[] Prepare()
    {
        WritePrivateKey(key.ExportECPrivateKeyPem());
        var now = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        var payload = new PvadLicensePayload(1, Guid.NewGuid(), "synthetic-customer-do-not-log", PvadLicenseModel.Trial,
            now, now, now.AddDays(30), null, false, [new("pdf", [])]);
        File.WriteAllBytes(Path.Combine(directory, "payload.json"), JsonSerializer.SerializeToUtf8Bytes(payload, PvadLicenseJson.Options));
        return ["--payload", Path.Combine(directory, "payload.json"), "--private-key", Path.Combine(directory, "key.pem"),
            "--key-id", "synthetic", "--output", Path.Combine(directory, "license.json")];
    }

    private void WritePrivateKey(string pem)
    {
        var path = Path.Combine(directory, "key.pem");
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using var file = new FileStream(path, options);
        using var writer = new StreamWriter(file);
        writer.Write(pem);
    }

    public void Dispose()
    {
        key.Dispose();
        Directory.Delete(directory, recursive: true);
    }
}
