using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.Api.Tests;

/// <summary>Explicit test-owned keys, created outside application state and removed with the service provider.</summary>
internal sealed class PvadSyntheticLicenseIssuer : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "pvad-test-license-issuer-" + Guid.NewGuid().ToString("N"));
    internal string PrivatePath => Path.Combine(root, "private.pem");
    internal string PublicPath => Path.Combine(root, "public.pem");

    public PvadSyntheticLicenseIssuer()
    {
        Directory.CreateDirectory(root);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var file = new FileStream(PrivatePath, options))
        using (var writer = new StreamWriter(file)) writer.Write(key.ExportECPrivateKeyPem());
        File.WriteAllText(PublicPath, key.ExportSubjectPublicKeyInfoPem());
    }

    internal static void Configure(IServiceCollection services)
    {
        services.AddSingleton<PvadSyntheticLicenseIssuer>();
        services.AddSingleton<IPostConfigureOptions<PvadLicensingOptions>, ConfigureOptions>();
    }

    private sealed class ConfigureOptions : IPostConfigureOptions<PvadLicensingOptions>
    {
        private readonly PvadSyntheticLicenseIssuer issuer;
        public ConfigureOptions(PvadSyntheticLicenseIssuer issuer) => this.issuer = issuer;
        public void PostConfigure(string? name, PvadLicensingOptions options)
        {
            options.IssuerEnabled = true;
            options.KeyId = "pvad-synthetic-test-issuer";
            options.PrivateKeyPath = issuer.PrivatePath;
            options.PublicKeyPath = issuer.PublicPath;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
