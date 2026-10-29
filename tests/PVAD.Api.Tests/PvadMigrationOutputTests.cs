using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Pdf.Viewer.Nutrient;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Services;

namespace PVAD.Api.Tests;

public sealed class PvadMigrationOutputTests
{
    [Fact]
    public void Registry_writes_only_canonical_artifact_identities_for_every_provider()
    {
        foreach (var provider in MigrationProviderCatalog.All)
        foreach (var artifact in provider.Artifacts)
        {
            if (artifact.Id == "design") continue;
            var identity = MigrationArtifactIdentities.Resolve(artifact.Id, artifact.MediaType, artifact.FileExtension);
            Assert.NotNull(identity);
            Assert.Equal(identity.Id, artifact.Id);
            Assert.Equal(identity.MediaType, artifact.MediaType);
            Assert.Equal(identity.FileExtension, artifact.FileExtension);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Catalog_and_provider_output_agree_without_mutating_registry(bool legacy)
    {
        var controller = Controller(legacy);
        var catalog = Value<MigrationCatalogResponse>(controller.GetCatalog());
        Assert.Equal(MigrationProviderCatalog.All.Count, catalog.Providers.Count);
        foreach (var provider in catalog.Providers)
        {
            var detail = Value<MigrationProviderResponse>(controller.GetProvider(provider.Domain, provider.Kind, provider.ProviderKey));
            Assert.Equal(provider.Artifacts, detail.Artifacts);
            foreach (var artifact in provider.Artifacts)
            {
                if (artifact.Id == "design") continue;
                Assert.StartsWith(legacy ? "pxa-" : "pvad-", artifact.Id);
                Assert.StartsWith("pvad-", artifact.CanonicalId);
            }
        }
        Registry_writes_only_canonical_artifact_identities_for_every_provider();
    }

    [Theory]
    [InlineData("pdf", false)]
    [InlineData("pdf", true)]
    [InlineData("spreadsheet", false)]
    [InlineData("spreadsheet", true)]
    public void Code_conversions_match_catalog_and_keep_code_bytes(string domain, bool legacy)
    {
        var provider = domain == "pdf" ? "DevExpress" : "ClosedXmlSpreadsheet";
        var source = domain == "pdf"
            ? "using DevExpress.Pdf; var processor = new PdfDocumentProcessor(); processor.CreateEmptyDocument();"
            : "using ClosedXML.Excel; var workbook = new XLWorkbook(); var sheet = workbook.Worksheets.Add(\"Sales\");";
        var request = new UnifiedMigrationRequest("csharp", source);
        var controller = Controller(legacy);
        var response = Value<UnifiedMigrationResponse>(controller.Convert(domain, "code", provider, request));
        var artifact = Assert.Single(response.Artifacts);
        var descriptor = Assert.Single(Value<MigrationProviderResponse>(controller.GetProvider(domain, "code", provider)).Artifacts);
        Assert.Equal(descriptor.Id, artifact.Id);
        Assert.Equal(descriptor.MediaType, artifact.MediaType);
        Assert.Equal(descriptor.FileExtension, artifact.FileExtension);
        Assert.Contains("PVAD.", Assert.IsType<string>(artifact.Content));
        var current = Value<UnifiedMigrationResponse>(Controller(false).Convert(domain, "code", provider, request));
        Assert.Equal(Assert.Single(current.Artifacts).Content, artifact.Content);
        Assert.Equal(domain == "pdf" ? "devexpress-pdf" : "closedxml", response.Provider);
    }

    [Theory]
    [InlineData("instantJson", false)]
    [InlineData("instantJson", true)]
    [InlineData("javascript", false)]
    [InlineData("javascript", true)]
    public void Viewer_writers_match_complete_identity_tuple_and_preserve_payload(string inputType, bool legacy)
    {
        var request = new UnifiedMigrationRequest(inputType,
            inputType == "instantJson"
                ? """{"format":"https://pspdfkit.com/instant-json/v1","annotations":[]}"""
                : "PSPDFKit.load({ container: '#viewer', document: '/documents/report.pdf' });",
            Pages: [new NutrientPageGeometry(595, 842)]);
        var controller = Controller(legacy);
        var response = Value<UnifiedMigrationResponse>(controller.Convert("pdf", "viewer", "PSPDFKit", request));
        var artifact = Assert.Single(response.Artifacts);
        var descriptor = Assert.Single(Value<MigrationProviderResponse>(controller.GetProvider("pdf", "viewer", "nutrient-viewer")).Artifacts);
        Assert.Equal(descriptor.Id, artifact.Id);
        Assert.Equal(descriptor.MediaType, artifact.MediaType);
        Assert.Equal(descriptor.FileExtension, artifact.FileExtension);
        Assert.Equal("pvad-viewer-package", artifact.CanonicalId);
        var package = Assert.IsType<JsonObject>(artifact.Content);
        Assert.Equal(legacy ? MigrationArtifactIdentities.LegacyViewerPackageKind : MigrationArtifactIdentities.ViewerPackageKind,
            package["kind"]!.GetValue<string>());
        Assert.Equal(MigrationArtifactIdentities.ViewerPackageKind, package["canonicalKind"]!.GetValue<string>());
        Assert.Equal(1, package["schemaVersion"]!.GetValue<int>());

        var current = Value<UnifiedMigrationResponse>(Controller(false).Convert("pdf", "viewer", "nutrient-viewer", request));
        var currentPackage = Assert.IsType<JsonObject>(Assert.Single(current.Artifacts).Content).DeepClone();
        var comparablePackage = package.DeepClone();
        if (inputType == "instantJson")
        {
            // Separate conversions retain their own export timestamp and corresponding hashes.
            foreach (var value in new[] { currentPackage, comparablePackage })
            {
                var sidecar = Assert.IsType<JsonObject>(value["annotationSidecar"]);
                Assert.True(DateTimeOffset.TryParse(sidecar["exportedAt"]!.GetValue<string>(), out _));
                sidecar.Remove("exportedAt");
            }
        }
        currentPackage["kind"] = package["kind"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(comparablePackage, currentPackage));
        Assert.Equal(current.Checksums!["source"], response.Checksums!["source"]);
        Assert.All(response.Checksums.Values, checksum => Assert.Matches("^[a-f0-9]{64}$", checksum));
        if (inputType != "instantJson") Assert.Equal(current.Checksums, response.Checksums);
    }

    [Fact]
    public async Task Rewritten_legacy_request_keeps_legacy_output_even_under_canonical_path()
    {
        var controller = Controller(true);
        await new PvadLegacyApiMiddleware(context =>
        {
            Assert.StartsWith("/api/pvad/v1", context.Request.Path.Value);
            var provider = Value<MigrationProviderResponse>(controller.GetProvider("pdf", "viewer", "nutrient-viewer"));
            Assert.Equal("pxa-viewer-package", Assert.Single(provider.Artifacts).Id);
            return Task.CompletedTask;
        }).InvokeAsync(controller.HttpContext);
        Assert.StartsWith("/api/pxa/v1", controller.Request.Path.Value);
    }

    [Fact]
    public void Json_response_serializes_current_primary_and_canonical_fields_consistently()
    {
        var response = Value<MigrationProviderResponse>(Controller(false).GetProvider("pdf", "viewer", "nutrient-viewer"));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var artifact = json.RootElement.GetProperty("artifacts")[0];
        foreach (var (primary, canonical) in new[] { ("id", "canonicalId"), ("mediaType", "canonicalMediaType"), ("fileExtension", "canonicalFileExtension") })
            Assert.Equal(artifact.GetProperty(primary).GetString(), artifact.GetProperty(canonical).GetString());
    }

    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static UnifiedMigrationsController Controller(bool legacy)
    {
        var controller = new UnifiedMigrationsController(new MigrationService(), new ReportMigrationService(), new NutrientViewerDesignMigrationService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.Request.Path = legacy ? "/api/pxa/v1/migrations/catalog" : "/api/pvad/v1/migrations/catalog";
        return controller;
    }
}
