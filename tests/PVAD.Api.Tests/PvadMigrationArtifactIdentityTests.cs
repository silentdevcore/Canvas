using PVAD.Migration.Abstractions;
using PVAD.WebApi.Controllers;

namespace PVAD.Api.Tests;

public sealed class PvadMigrationArtifactIdentityTests
{
    [Fact]
    public async Task ActualOpenApi_DescribesMigrationArtifactsAndCanonicalMetadata()
    {
        using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var document = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "MigrationArtifactDescriptor", "MigrationArtifactResponse" })
        {
            var properties = schemas.GetProperty(name).GetProperty("properties");
            Assert.True(properties.TryGetProperty("id", out _));
            Assert.True(properties.TryGetProperty("canonicalId", out _));
            Assert.True(properties.TryGetProperty("canonicalMediaType", out _));
            Assert.True(properties.TryGetProperty("canonicalFileExtension", out _));
        }
        Assert.True(schemas.GetProperty("MigrationDiagnosticResponse").GetProperty("properties")
            .TryGetProperty("canonicalCode", out _));
        var response = document.RootElement.GetProperty("paths").GetProperty("/api/pvad/v1/migrations/catalog")
            .GetProperty("get").GetProperty("responses").GetProperty("200");
        Assert.Contains("MigrationCatalogResponse", response.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pxa-csharp", "text/x-csharp", ".cs", "pvad-csharp")]
    [InlineData("pvad-csharp", "text/x-csharp", ".cs", "pvad-csharp")]
    [InlineData("pxa-spreadsheet-csharp", "text/x-csharp", ".cs", "pvad-spreadsheet-csharp")]
    [InlineData("pvad-spreadsheet-csharp", "text/x-csharp", ".cs", "pvad-spreadsheet-csharp")]
    [InlineData("pxa-viewer-package", "application/vnd.pxa.viewer+json", ".pxaviewer.json", "pvad-viewer-package")]
    [InlineData("pvad-viewer-package", "application/vnd.pvad.viewer+json", ".pvadviewer.json", "pvad-viewer-package")]
    public void ReviewedTuples_HaveMatchingCatalogAndResponseAliases(string id, string mime, string extension, string canonical)
    {
        var descriptor = new MigrationArtifactDescriptor(id, mime, extension);
        var response = new MigrationArtifactResponse(id, mime, extension, "unchanged-content");
        Assert.Equal(id, response.Id);
        Assert.Equal(mime, response.MediaType);
        Assert.Equal(extension, response.FileExtension);
        Assert.Equal("unchanged-content", response.Content);
        Assert.Equal(canonical, descriptor.CanonicalId);
        Assert.Equal(descriptor.CanonicalId, response.CanonicalId);
        Assert.Equal(descriptor.CanonicalMediaType, response.CanonicalMediaType);
        Assert.Equal(descriptor.CanonicalFileExtension, response.CanonicalFileExtension);
    }

    [Theory]
    [InlineData("pxa-viewer-package", "application/vnd.pvad.viewer+json", ".pxaviewer.json")]
    [InlineData("pxa-viewer-package", "application/vnd.pxa.viewer+json", ".zip")]
    [InlineData("design", "application/json", ".json")]
    [InlineData("pxa-unknown", "application/json", ".json")]
    public void MixedUnknownAndNeutralTuples_AreNotReclassified(string id, string mime, string extension) =>
        Assert.Null(MigrationArtifactIdentities.Resolve(id, mime, extension));

    [Fact]
    public void Legacy_projection_roundtrips_each_known_identity_and_does_not_reclassify_unknowns()
    {
        foreach (var canonical in new[] { MigrationArtifactIdentities.PdfCode, MigrationArtifactIdentities.SpreadsheetCode, MigrationArtifactIdentities.ViewerPackage })
        {
            Assert.Equal(canonical, MigrationArtifactIdentities.ForOutput(canonical, false));
            var legacy = MigrationArtifactIdentities.ForOutput(canonical, true);
            Assert.StartsWith("pxa-", legacy.Id);
            Assert.Equal(canonical, MigrationArtifactIdentities.Resolve(legacy.Id, legacy.MediaType, legacy.FileExtension));
        }
        var neutral = new MigrationArtifactIdentity("design", "application/json", ".json");
        Assert.Same(neutral, MigrationArtifactIdentities.ForOutput(neutral, true));
    }

    [Fact]
    public void ProviderDiagnostics_KeepStableCanmigIds()
    {
        var diagnostic = new MigrationDiagnosticResponse("CANMIGNUTWEB030", "warning", "Review required");
        Assert.Equal("CANMIGNUTWEB030", diagnostic.Code);
        Assert.Null(diagnostic.CanonicalCode);
        var apiDiagnostic = new MigrationDiagnosticResponse("PXAMIG003", "error", "Source required");
        Assert.Equal("PVADMIG003", apiDiagnostic.CanonicalCode);
    }
}
