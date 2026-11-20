using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Licensing;

namespace PVAD.LicenseIssuer;

/// <summary>
/// Manufacturer-only offline preparation tool. Operator authorization is supplied
/// by access to the private-key file; this is not a customer-facing issuance API.
/// </summary>
internal static class NativeLicenseIssuer
{
    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var revocations = args.Length > 0 && args[0] == "revocations";
        if (revocations) args = args[1..];
        if (args.Length != 8)
            return Fail("Usage: [revocations] --payload <file> --private-key <file> --key-id <id> --output <new-file>", error);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (args[index] is not ("--payload" or "--private-key" or "--key-id" or "--output") ||
                string.IsNullOrWhiteSpace(args[index + 1]) || !options.TryAdd(args[index], args[index + 1]))
                return Fail("Invalid or duplicate issuer arguments.", error);
        }
        if (options["--key-id"].Length > 100 || options["--key-id"].Any(char.IsControl))
            return Fail("Invalid issuer key identifier.", error);

        try
        {
            using var payloadStream = File.OpenRead(options["--payload"]);
            var payloadBytes = PvadLicenseJson.ReadBounded(payloadStream);
            if (revocations)
                PvadLicenseVerifier.ValidateRevocations(PvadLicenseJson.Read<PvadNativeRevocationList>(payloadBytes, PvadLicenseJson.MaximumPayloadBytes));
            else
                PvadLicenseVerifier.ValidatePayload(PvadLicenseJson.Read<PvadLicensePayload>(payloadBytes, PvadLicenseJson.MaximumPayloadBytes));
            var keyPath = options["--private-key"];
            if (!OperatingSystem.IsWindows() &&
                (File.GetUnixFileMode(keyPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                    UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                return Fail("The issuer private key must not be accessible by group or other users.", error);
            using var keyStream = File.OpenRead(keyPath);
            if (keyStream.Length is < 1 or > 16 * 1024)
                return Fail("Invalid issuer private key.", error);
            using var keyReader = new StreamReader(keyStream, new UTF8Encoding(false, true));
            using var signer = ECDsa.Create();
            signer.ImportFromPem(keyReader.ReadToEnd());
            var privateParameters = signer.ExportParameters(true);
            try
            {
                if (privateParameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value || privateParameters.D is null)
                    return Fail("The issuer requires an ECDSA P-256 private key.", error);
            }
            finally
            {
                if (privateParameters.D is not null)
                    CryptographicOperations.ZeroMemory(privateParameters.D);
            }
            var signature = signer.SignData(payloadBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var serialized = revocations
                ? JsonSerializer.SerializeToUtf8Bytes(new PvadSignedNativeRevocationList(1, PvadNativeRevocationList.DocumentKind,
                    options["--key-id"], PvadLicenseJson.Algorithm, Convert.ToBase64String(payloadBytes), Convert.ToBase64String(signature)), PvadLicenseJson.Options)
                : JsonSerializer.SerializeToUtf8Bytes(new PvadSignedNativeLicense(1, options["--key-id"], PvadLicenseJson.Algorithm,
                    Convert.ToBase64String(payloadBytes), Convert.ToBase64String(signature)), PvadLicenseJson.Options);
            var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
                fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using var file = new FileStream(options["--output"], fileOptions);
            file.Write(serialized);
            file.Flush(flushToDisk: true);
            output.WriteLine(revocations
                ? "Native revocation artifact created. Authorization, durable sequencing and distribution are separate operator responsibilities."
                : "Native license artifact created. Publication and trust-key approval are separate steps.");
            return 0;
        }
        catch (PvadLicenseException exception)
        {
            return Fail($"{exception.Code}: Native license issuance was rejected.", error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            CryptographicException or ArgumentException or JsonException)
        {
            return Fail("Native license issuance failed; no key or payload details are logged.", error);
        }
    }

    private static int Fail(string message, TextWriter error)
    {
        error.WriteLine(message);
        return 1;
    }
}
