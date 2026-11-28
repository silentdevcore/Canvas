using System.Runtime.CompilerServices;
using PVAD.Licensing;

internal static class PvadNativeLicenseTestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var path = Environment.GetEnvironmentVariable("PVAD_NATIVE_LICENSE_PATH");
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("The isolated test run requires a signed native license.");
        PvadLicense.LoadFromFile(path);
    }
}
