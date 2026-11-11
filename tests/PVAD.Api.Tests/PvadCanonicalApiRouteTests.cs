using System.Net;
using System.Net.Http.Json;

namespace PVAD.Api.Tests;

public sealed class PvadCanonicalApiRouteTests(PvadApiTestFactory factory)
    : IClassFixture<PvadApiTestFactory>
{
    [Theory]
    [InlineData("/api/pxa/v1/version")]
    [InlineData("/API/PXA/V1/version")]
    [InlineData("/api/pxa/export/formats")]
    [InlineData("/api/pxa/migration/frameworks")]
    [InlineData("/api/pxa/templates")]
    public async Task Former_route_prefixes_are_not_served(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(response.Headers.Contains("Deprecation"));
    }

    [Fact]
    public async Task Canonical_version_route_has_no_deprecation_headers()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/pvad/v1/version");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Deprecation"));
    }

    [Fact]
    public async Task OpenApi_advertises_only_canonical_routes()
    {
        using var client = factory.CreateOpenApiClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var paths = document.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToArray();
        Assert.Contains("/api/pvad/v1/version", paths);
        Assert.Contains("/api/pvad/v1/designer/templates", paths);
        Assert.Contains("/api/pvad/spreadsheet/export", paths);
        Assert.Contains("/api/pvad/templates/render-design", paths);
        Assert.DoesNotContain(paths, path => path.StartsWith("/api/pxa/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Generated_OpenApi_contract_snapshot_is_exported_for_tooling()
    {
        using var client = factory.CreateOpenApiClient();
        var source = await client.GetStringAsync("/openapi/v1.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(source)!;
        document["servers"] = new System.Text.Json.Nodes.JsonArray(
            new System.Text.Json.Nodes.JsonObject { ["url"] = "/" });
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var output = Path.Combine(directory.FullName, "artifacts", "openapi", "openapi.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, document.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
    }
}
