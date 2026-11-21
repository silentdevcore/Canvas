using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PVAD.CodeWorker;
using PVAD.Core.Contracts;
using PVAD.Licensing;
using PVAD.Testing;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.Api.Tests;

public sealed class PvadNativePdfLicenseTests
{
    [Fact]
    public async Task Api_reports_host_license_failure_without_exposing_paths_or_confusing_user_authorization()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "secret-license.json");
        var exception = Assert.Throws<PvadLicenseException>(() => PvadLicense.LoadFromFile(missingPath));
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddProblemDetails();
                services.AddExceptionHandler<PvadNativeLicenseExceptionHandler>();
            })
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.Run(_ => throw exception);
            }));
        var response = await server.CreateClient().GetAsync("/native-license-probe");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("PVADLIC013", problem.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(missingPath, body);
        Assert.Contains("operator", body);
    }

    [Fact]
    public async Task Host_without_configuration_can_start_for_account_and_admin_without_creating_keys_or_licenses()
    {
        var bootstrap = new PvadNativeLicenseBootstrap(Options.Create(new PvadNativeLicenseOptions()), new TestEnvironment());
        await bootstrap.StartAsync(default);
        await bootstrap.StopAsync(default);
    }

    [Fact]
    public async Task Explicitly_configured_invalid_native_license_fails_startup_and_keeps_active_snapshot()
    {
        var before = PvadLicense.GetStatus();
        var bootstrap = new PvadNativeLicenseBootstrap(Options.Create(new PvadNativeLicenseOptions
        {
            LicensePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json"),
        }), new TestEnvironment());
        var error = await Assert.ThrowsAsync<PvadLicenseException>(() => bootstrap.StartAsync(default));
        Assert.Equal("PVADLIC013", error.Code);
        Assert.Equal(before, PvadLicense.GetStatus());
    }

    [Theory]
    [InlineData("missing", "PVADLIC001")]
    [InlineData("word", "PVADLIC012")]
    [InlineData("expired", "PVADLIC009")]
    public async Task Worker_returns_native_diagnostic_and_no_design_or_bytes_for_unlicensed_pdf(string license, string code)
    {
        var response = await Worker(license).RunAsync(Request(), default);
        Assert.False(response.Success);
        Assert.Null(response.CanonicalDesign);
        Assert.Null(response.PdfBytes);
        Assert.Contains(response.Diagnostics, item => item.Code == code);
        Assert.DoesNotContain(response.Diagnostics, item => item.Code == "PVADCODE042");
    }

    [Fact]
    public async Task Worker_accepts_explicit_signed_pdf_license_and_retains_legacy_pdf_execution()
    {
        var response = await Worker("pdf").RunAsync(Request(), default);
        Assert.True(response.Success, string.Join(", ", response.Diagnostics.Select(value => value.Code)));
        Assert.NotNull(response.CanonicalDesign);
        Assert.NotNull(response.PdfBytes);
        Assert.True(response.PdfBytes!.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
    }

    [Fact]
    public async Task Worker_default_resolves_the_running_test_artifact_not_a_stale_regular_build()
    {
        var response = await Worker("pdf", useDefaultPath: true).RunAsync(Request(), default);
        Assert.True(response.Success, string.Join(", ", response.Diagnostics.Select(value => value.Code)));
        Assert.NotEmpty(response.PdfBytes!);
    }

    [Theory]
    [InlineData("return PVAD.Licensing.PvadLicense.GetStatus();")]
    [InlineData("return PvadLicense.LoadFromFile(\"forbidden.json\");")]
    public void Script_cannot_control_the_host_license(string source) =>
        Assert.Contains(SandboxPolicy.Analyze(source), item => item.Severity == "error");

    [Theory]
    [InlineData("return System.Environment.GetEnvironmentVariables();")]
    [InlineData("return System.IO.File.ReadAllText(\"license.json\");")]
    [InlineData("return System.Reflection.Assembly.Load(\"PVAD.Licensing\");")]
    [InlineData("return System.Diagnostics.Process.Start(\"dotnet\");")]
    [InlineData("return PVAD.Licensing.PvadLicense.GetStatus();")]
    [InlineData("PvadLicense.LoadFromFile(\"license.json\"); return new PdfDocument();")]
    [InlineData("using License = PVAD.Licensing.PvadLicense; return License.GetStatus();")]
    public async Task Worker_process_rejects_user_control_of_licensing_and_host_configuration(string source)
    {
        var request = Request();
        request.Source = source;
        var response = await Worker("pdf").RunAsync(request, default);
        Assert.False(response.Success);
        Assert.Null(response.CanonicalDesign);
        Assert.Null(response.PdfBytes);
        Assert.Contains(response.Diagnostics, item => item.Severity == "error" && (item.Code is "PVADCODE006" or "PVADCODE008"));
        Assert.DoesNotContain(response.Diagnostics, item => item.Code is "PVADCODE041" or "PVADCODE042");
    }

    [Theory]
    [InlineData("missing", "PVADLIC001")]
    [InlineData("word", "PVADLIC012")]
    public async Task User_request_fields_cannot_supply_native_rights_or_enable_server_mode(string license, string diagnostic)
    {
        var configured = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")!;
        var licenseDirectory = Path.GetDirectoryName(configured)!;
        var directory = Directory.CreateTempSubdirectory("pvad-worker-license-request-").FullName;
        using var process = new System.Diagnostics.Process();
        var started = false;
        try
        {
            var input = Path.Combine(directory, "request.json");
            var output = Path.Combine(directory, "response.json");
            await File.WriteAllTextAsync(input, JsonSerializer.Serialize(new
            {
                language = "csharpPdf", operation = "execute", source = Request().Source,
                serverMode = true, licensePath = configured,
                nativeLicensePath = configured, nativeLicense = await File.ReadAllTextAsync(configured),
                nativeLicensing = new { licensePath = configured },
            }));
            process.StartInfo = new("dotnet") { UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, WorkingDirectory = directory };
            process.StartInfo.ArgumentList.Add(typeof(SandboxPolicy).Assembly.Location);
            process.StartInfo.ArgumentList.Add(input);
            process.StartInfo.ArgumentList.Add(output);
            process.StartInfo.Environment.Clear();
            process.StartInfo.Environment["DOTNET_EnableDiagnostics"] = "0";
            if (license != "missing")
                process.StartInfo.Environment["PVAD_NATIVE_LICENSE_PATH"] = Path.Combine(licenseDirectory, license + ".json");
            Assert.True(started = process.Start());
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var standardOutput = await stdout;
            var standardError = await stderr;
            var body = await File.ReadAllTextAsync(output);
            var response = JsonSerializer.Deserialize<PvadCodeWorkerResponse>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.False(response.Success);
            Assert.Null(response.CanonicalDesign);
            Assert.Null(response.PdfBytes);
            Assert.Contains(response.Diagnostics, item => item.Code == diagnostic);
            Assert.DoesNotContain(configured, body);
            Assert.DoesNotContain(configured, standardOutput + standardError);
        }
        finally
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            Directory.Delete(directory, recursive: true);
        }
    }

    private static PvadCodeWorkerRequest Request() => new()
    {
        Language = PvadCodeLanguages.CSharpPdf, Operation = "execute",
        Source = "var document = new PdfDocument(); document.AddPage().DrawText(\"Synthetic\", 40, 800, 18); return document;",
    };

    private static PvadCodeWorkerClient Worker(string license, bool useDefaultPath = false)
    {
        var configured = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Run with tools/native-licensing/with-test-license.mjs.");
        return new(new TestEnvironment(), Options.Create(new PvadCodeWorkerOptions
        {
            Enabled = true, WorkerPath = useDefaultPath ? null : Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location),
        }), NullLogger<PvadCodeWorkerClient>.Instance, Options.Create(new PvadNativeLicenseOptions
        {
            LicensePath = license == "missing" ? null : Path.Combine(Path.GetDirectoryName(configured)!, license + ".json"),
        }));
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PVAD.WebApi";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
