using PVAD.Migration.Abstractions;
using PVAD.WebApi.Controllers;

namespace PVAD.Api.Tests;

public sealed class PvadMigrationArtifactIdentityTests
{
    [Fact]
    public async Task ActualOpenApi_DescribesMigrationArtifactsWithoutAliasMetadata()
    {
        using var factory = new PvadApiTestFactory();
        using var client = factory.CreateOpenApiClient();
        using var document = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "MigrationArtifactDescriptor", "MigrationArtifactResponse" })
        {
            var properties = schemas.GetProperty(name).GetProperty("properties");
            Assert.True(properties.TryGetProperty("id", out _));
            Assert.False(properties.TryGetProperty("canonicalId", out _));
        }
        Assert.False(schemas.GetProperty("MigrationDiagnosticResponse").GetProperty("properties")
            .TryGetProperty("canonicalCode", out _));
        var response = document.RootElement.GetProperty("paths").GetProperty("/api/pvad/v1/migrations/catalog")
            .GetProperty("get").GetProperty("responses").GetProperty("200");
        Assert.Contains("MigrationCatalogResponse", response.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pvad-csharp", "text/x-csharp", ".cs")]
    [InlineData("pvad-spreadsheet-csharp", "text/x-csharp", ".cs")]
    [InlineData("pvad-viewer-package", "application/vnd.pvad.viewer+json", ".pvadviewer.json")]
    public void Canonical_tuples_resolve_to_themselves(string id, string mime, string extension)
    {
        var identity = MigrationArtifactIdentities.Resolve(id, mime, extension);
        Assert.Equal(new MigrationArtifactIdentity(id, mime, extension), identity);
    }

    [Theory]
    [InlineData("pvad-viewer-package", "application/vnd.pvad.viewer+json", ".zip")]
    [InlineData("pvad-csharp", "application/json", ".cs")]
    [InlineData("design", "application/json", ".json")]
    [InlineData("pvad-unknown", "application/json", ".json")]
    public void MixedUnknownAndNeutralTuples_AreNotReclassified(string id, string mime, string extension) =>
        Assert.Null(MigrationArtifactIdentities.Resolve(id, mime, extension));

    [Theory]
    [InlineData("pxa-csharp", "text/x-csharp", ".cs")]
    [InlineData("pxa-spreadsheet-csharp", "text/x-csharp", ".cs")]
    [InlineData("pxa-viewer-package", "application/vnd.pxa.viewer+json", ".pxaviewer.json")]
    public void Former_artifact_tuples_are_not_resolved(string id, string mime, string extension) =>
        Assert.Null(MigrationArtifactIdentities.Resolve(id, mime, extension));

    [Fact]
    public void ProviderDiagnostics_KeepStableCanmigIds()
    {
        var diagnostic = new MigrationDiagnosticResponse("CANMIGNUTWEB030", "warning", "Review required");
        Assert.Equal("CANMIGNUTWEB030", diagnostic.Code);
    }
}
