using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PVAD.Core.Contracts;
using PVAD.FileImporter.ImageOcr;
using PVAD.Licensing;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Services.Licensing;

// A child of the real OCR runner verifies the exact signed configuration handoff.
if (args.Length == 2)
{
    var path = Environment.GetEnvironmentVariable("PVAD_NATIVE_REVOCATIONS_PATH");
    if (path is not null) PvadLicense.ImportRevocationsFromFile(path);
    PvadLicense.LoadFromFile(Environment.GetEnvironmentVariable("PVAD_NATIVE_LICENSE_PATH")!);
    PvadLicense.Require(PvadLicenseProducts.Importer);
    Console.Write(JsonSerializer.Serialize(PvadLicense.GetRevocationStatus()));
    return 0;
}
if (args.Length != 6) return 64;
var checks = new List<string>();
void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
var directory = Directory.CreateTempSubdirectory("pvad-revocation-probe-").FullName;
var inherited = Environment.GetEnvironmentVariable("PVAD_NATIVE_REVOCATIONS_PATH");
try
{
    Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "starts-without-implicit-activation");
    PvadLicense.LoadFromFile(args[0]);
    var ownDll = Assembly.GetExecutingAssembly().Location;
    var runner = new DefaultOcrWorkerProcessRunner();
    Environment.SetEnvironmentVariable("PVAD_NATIVE_REVOCATIONS_PATH", Path.Combine(directory, "untrusted-parent.json"));
    var clean = await runner.RunAsync(ownDll, "unused", "unused", directory, TimeSpan.FromSeconds(15), default);
    using var cleanReport = JsonDocument.Parse(clean.StandardOutput);
    Check(clean.ExitCode == 0 && !cleanReport.RootElement.GetProperty("Loaded").GetBoolean(), "ocr-does-not-inherit-unverified-parent-revocations");
    PvadLicense.ImportRevocationsFromFile(args[3]);
    Check(PvadLicense.GetRevocationStatus().RevokedLicenseCount == 2, "explicit-signed-import");
    Check(PvadLicense.Require("pdf").LicenseId == PvadLicense.GetStatus().LicenseId, "unrelated-license-remains-valid");
    var propagated = await runner.RunAsync(ownDll, "unused", "unused", directory, TimeSpan.FromSeconds(15), default);
    using var report = JsonDocument.Parse(propagated.StandardOutput);
    Check(propagated.ExitCode == 0 && report.RootElement.GetProperty("Sequence").GetInt64() == 1 &&
        report.RootElement.GetProperty("PayloadSha256").GetString() == PvadLicense.GetRevocationStatus().PayloadSha256, "ocr-runner-propagates-the-verified-list");
    Check(!Directory.EnumerateFiles(directory).Any(), "ocr-handoff-files-removed");

    var environment = new ProbeEnvironment(directory);
    var before = PvadLicense.GetStatus();
    var invalidBootstrap = new PvadNativeLicenseBootstrap(Options.Create(new PvadNativeLicenseOptions
        { LicensePath = args[0], RevocationListPath = Path.Combine(directory, "missing-list.json") }), environment);
    try { await invalidBootstrap.StartAsync(default); throw new InvalidOperationException("invalid configured list accepted"); }
    catch (PvadLicenseException error) { Check(error.Code == "PVADLIC013", "api-startup-rejects-unreadable-configured-list"); }
    Check(PvadLicense.GetStatus() == before, "failed-startup-does-not-discard-existing-valid-license");
    var request = new PvadCodeWorkerRequest { Language = PvadCodeLanguages.CSharpPdf, Operation = "execute",
        Source = "var document = new PdfDocument(); document.AddPage().DrawText(\"Synthetic\", 40, 800, 18); return document;" };
    PvadCodeWorkerClient Client(string license, string? list = null) => new(environment,
        Options.Create(new PvadCodeWorkerOptions { Enabled = true, WorkerPath = args[4] }),
        NullLogger<PvadCodeWorkerClient>.Instance, Options.Create(new PvadNativeLicenseOptions { LicensePath = license, RevocationListPath = list }));
    var denied = await Client(args[1]).RunAsync(request, default);
    Check(!denied.Success && denied.CanonicalDesign is null && denied.PdfBytes is null && denied.Diagnostics.Any(item => item.Code == "PVADLIC014"), "code-worker-rejects-revoked-configured-license-without-executing-source");
    var accepted = await Client(args[0]).RunAsync(request, default);
    Check(accepted.Success && accepted.PdfBytes is { Length: > 5 } && Encoding.ASCII.GetString(accepted.PdfBytes, 0, 5) == "%PDF-", "code-worker-keeps-valid-pdf-execution");

    var output = Path.Combine(directory, "ocr-response.json");
    using var process = new Process { StartInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true } };
    process.StartInfo.ArgumentList.Add(args[5]); process.StartInfo.ArgumentList.Add(Path.Combine(directory, "never-read-input.json")); process.StartInfo.ArgumentList.Add(output);
    process.StartInfo.Environment["PVAD_NATIVE_LICENSE_PATH"] = args[2]; process.StartInfo.Environment["PVAD_NATIVE_REVOCATIONS_PATH"] = args[3];
    process.Start();
    try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
    catch { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } throw; }
    using var ocr = JsonDocument.Parse(await File.ReadAllTextAsync(output));
    Check(process.ExitCode == 1 && ocr.RootElement.GetProperty("licenseErrorCode").GetString() == "PVADLIC014", "real-ocr-worker-rejects-before-reading-input");
    try { PvadLicense.LoadFromFile(args[1]); throw new InvalidOperationException("revoked license loaded"); }
    catch (PvadLicenseException error) { Check(error.Code == "PVADLIC014", "native-pdf-license-reload-denied"); }
    Check(PvadLicense.GetStatus().CanStartOperation, "failed-reload-preserves-valid-license-and-revocations");
    Console.WriteLine(JsonSerializer.Serialize(new { syntheticOnly = true, checks }));
    return 0;
}
finally { Environment.SetEnvironmentVariable("PVAD_NATIVE_REVOCATIONS_PATH", inherited); Directory.Delete(directory, true); }

sealed class ProbeEnvironment(string root) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "RevocationEngineProbe";
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = root;
    public string WebRootPath { get; set; } = root;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
