using System.Net;
using System.Text.Json;
using PVAD.Licensing;
using PVAD.Testing;
using PVAD.WebApi.Services;
using PVAD.WebApi.Services.Converters;
using System.Text;

namespace PVAD.Api.Tests;

[Collection("Native spreadsheet license renewal")]
public sealed class PvadNativeCodeMigrationLicenseTests
{
    [Fact]
    public async Task Code_routes_require_native_provider_rights_and_recover_after_signed_renewal()
    {
        var integrated = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Run with a signed native test session.");
        using var factory = new PvadApiTestFactory();
        using var client = factory.CreateClient();
        try
        {
            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "pdf.json"));
            foreach (var (domain, provider, alias) in new[] { ("pdf", "aspose-pdf", "Aspose"), ("spreadsheet", "aspose-cells", "AsposeCells") })
            {
                foreach (var path in new[] { $"/api/pvad/v1/migrations/{domain}/code/{provider}/convert", $"/api/pvad/v1/migrations/{domain}/code/{alias}/convert" })
                {
                    using var response = await client.PostAsJsonAsync(path, new { source = "var value = 42;" });
                    await Denied(response);
                }
                using var legacy = await client.PostAsJsonAsync("/api/pvad/migration/convert", new { framework = alias, sourceCode = "var value = 42;" });
                await Denied(legacy);
            }
        }
        finally { PvadLicense.LoadFromFile(integrated); }

        foreach (var (domain, provider) in new[] { ("pdf", "aspose-pdf"), ("spreadsheet", "aspose-cells") })
        {
            using var recovered = await client.PostAsJsonAsync($"/api/pvad/v1/migrations/{domain}/code/{provider}/convert", new { source = "var value = 42;" });
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
            Assert.Contains("42", await recovered.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Preview_rights_match_actual_processing_and_custom_callbacks_stay_fresh()
    {
        var integrated = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")!;
        var codeOnly = Path.Combine(Path.GetDirectoryName(integrated)!, "migration-code.json");
        using var factory = new PvadApiTestFactory();
        using var client = factory.CreateClient();
        var service = new MigrationService();
        try
        {
            PvadLicense.LoadFromFile(codeOnly);
            foreach (var framework in service.GetFrameworks().Where(framework => framework.MigrationKind == "code"))
            {
                Assert.Contains("42", service.Convert(framework.Id, "var value = 42;").PvadCode);
                if (framework.Domain == "spreadsheet")
                    Assert.Contains("<!doctype html>", Encoding.UTF8.GetString(service.GeneratePreview(framework.Id, "var value = 42;")));
                else
                    Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => service.GeneratePreview(framework.Id, "var value = 42;")).Code);
            }
            foreach (var product in new[] { "pdf", "spreadsheet", "converter" })
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => PvadLicense.Require(product)).Code);
            using var missingPdf = await client.PostAsJsonAsync("/api/pvad/migration/preview", new { framework = "Aspose", sourceCode = "var value = 42;" });
            await Denied(missingPdf);
            using var html = await client.PostAsJsonAsync("/api/pvad/migration/preview", new { framework = "AsposeCells", sourceCode = "var value = 42;" });
            Assert.Equal(HttpStatusCode.OK, html.StatusCode);
            Assert.Equal("text/html", html.Content.Headers.ContentType?.MediaType);

            PvadLicense.LoadFromFile(integrated);
            Assert.True(service.GeneratePreview("Aspose", "var value = 42;").AsSpan().StartsWith("%PDF-"u8));
            var custom = new CustomPreview(() => PvadLicense.LoadFromFile(codeOnly));
            // The public writer checks PDF rights again after the caller's drawing callback.
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => custom.GeneratePreview("synthetic")).Code);
            PvadLicense.LoadFromFile(integrated);
            var ownError = new IOException("Synthetic caller callback failure.");
            Assert.Same(ownError, Assert.Throws<IOException>(() => new CustomPreview(() => throw ownError).GeneratePreview("synthetic")));
        }
        finally { PvadLicense.LoadFromFile(integrated); }
    }

    private sealed class CustomPreview(Action action) : BasePdfConverter
    {
        public override string FrameworkId => "Aspose";
        public override string FrameworkName { get { action(); return "Synthetic caller preview"; } }
        public override string Description => "Synthetic native preview test";
        public override string ConvertCode(string sourceCode) => "document.AddPage();";
    }

    private static async Task Denied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Content.Headers.ContentDisposition);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PVADLIC012", problem.RootElement.GetProperty("code").GetString());
    }
}
