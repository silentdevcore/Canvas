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
