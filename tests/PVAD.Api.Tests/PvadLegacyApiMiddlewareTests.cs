using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadLegacyApiMiddlewareTests
{
    [Theory]
    [InlineData("/api/pxa/v1/designer/templates", "/api/pvad/v1/designer/templates")]
    [InlineData("/API/PXA/V1/version", "/api/pvad/v1/version")]
    [InlineData("/api/pxa/v1", "/api/pvad/v1")]
    [InlineData("/api/pxa/v10/version", "/api/pxa/v10/version")]
    [InlineData("/api/pxa/document/clone", "/api/pvad/document/clone")]
    [InlineData("/api/pxa/spreadsheet/validate", "/api/pvad/spreadsheet/validate")]
    [InlineData("/api/pxa/templates", "/api/pvad/templates")]
    [InlineData("/api/pxa/documentary", "/api/pxa/documentary")]
    [InlineData("/api/pxa/unknown", "/api/pxa/unknown")]
    [InlineData("/api/export", "/api/export")]
    [InlineData("/api/pvad/v1/version", "/api/pvad/v1/version")]
    public async Task Maps_only_the_legacy_prefix_without_changing_request_content(
        string original, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = original;
        context.Request.QueryString = new QueryString("?page=2");
        context.Request.Method = "POST";
        using var body = new MemoryStream([1, 2, 3]);
        context.Request.Body = body;
        context.Request.Headers.Authorization = "Bearer synthetic-test";
        var middleware = new PvadLegacyApiMiddleware(request =>
        {
            Assert.Equal(expected, request.Request.Path.Value);
            Assert.Equal("?page=2", request.Request.QueryString.Value);
            Assert.Equal("POST", request.Request.Method);
            Assert.Same(body, request.Request.Body);
            Assert.Equal("Bearer synthetic-test", request.Request.Headers.Authorization);
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(original, context.Request.Path.Value);
    }

    [Fact]
    public async Task Restores_original_path_when_downstream_fails()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/pxa/v1/version";
        var middleware = new PvadLegacyApiMiddleware(_ => throw new InvalidOperationException("test"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Equal("/api/pxa/v1/version", context.Request.Path.Value);
    }
}

public sealed class PvadApiPrefixIntegrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Version_alias_returns_the_same_payload_with_successor_headers()
    {
        using var client = factory.CreateClient();
        using var canonical = await client.GetAsync("/api/pvad/v1/version");
        using var legacy = await client.GetAsync("/api/pxa/v1/version");

        Assert.Equal(HttpStatusCode.OK, canonical.StatusCode);
        Assert.Equal(canonical.StatusCode, legacy.StatusCode);
        Assert.Equal(await canonical.Content.ReadAsStringAsync(), await legacy.Content.ReadAsStringAsync());
        Assert.False(canonical.Headers.Contains("Deprecation"));
        Assert.Equal("true", legacy.Headers.GetValues("Deprecation").Single());
        Assert.Contains("</api/pvad/v1/version>; rel=\"successor-version\"",
            legacy.Headers.GetValues("Link"));
    }

    [Theory]
    [InlineData("/admin/users")]
    [InlineData("/designer/templates")]
    [InlineData("/designer/preferences/element-favorites")]
    public async Task Both_prefixes_enforce_authentication(string suffix)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using var canonical = await client.GetAsync("/api/pvad/v1" + suffix);
        using var legacy = await client.GetAsync("/api/pxa/v1" + suffix);

        Assert.Equal(HttpStatusCode.Unauthorized, canonical.StatusCode);
        Assert.Equal(canonical.StatusCode, legacy.StatusCode);
        Assert.True(legacy.Headers.Contains("Deprecation"));
    }

    [Fact]
    public async Task Migration_catalog_alias_preserves_query_filters()
    {
        using var client = factory.CreateClient();
        const string suffix = "/migrations/catalog?domain=spreadsheet&kind=code";
        using var canonical = await client.GetAsync("/api/pvad/v1" + suffix);
        using var legacy = await client.GetAsync("/api/pxa/v1" + suffix);

        Assert.Equal(HttpStatusCode.OK, canonical.StatusCode);
        Assert.Equal(canonical.StatusCode, legacy.StatusCode);
        Assert.Equal(await canonical.Content.ReadAsStringAsync(), await legacy.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenApi_advertises_canonical_routes_without_duplicate_legacy_operations()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var paths = document.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToArray();
        Assert.Contains("/api/pvad/v1/version", paths);
        Assert.Contains("/api/pvad/v1/designer/templates", paths);
        Assert.DoesNotContain(paths, path => path.StartsWith("/api/pxa/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("/api/pvad/spreadsheet/export", paths);
        Assert.Contains("/api/pvad/templates/render-design", paths);
    }

    [Theory]
    [InlineData("/api/pvad/export/formats", "/api/pxa/export/formats")]
    [InlineData("/api/pvad/migration/frameworks", "/api/pxa/migration/frameworks")]
    public async Task Unversioned_aliases_use_the_same_controller_with_deprecation_headers(string canonicalPath, string legacyPath)
    {
        using var client = factory.CreateClient();
        using var canonical = await client.GetAsync(canonicalPath);
        using var legacy = await client.GetAsync(legacyPath);
        Assert.Equal(HttpStatusCode.OK, canonical.StatusCode);
        Assert.Equal(canonical.StatusCode, legacy.StatusCode);
        Assert.Equal(await canonical.Content.ReadAsStringAsync(), await legacy.Content.ReadAsStringAsync());
        Assert.True(legacy.Headers.Contains("Deprecation"));
        Assert.Contains(legacy.Headers.GetValues("Link"), value =>
            value.Contains($"<{canonicalPath}>; rel=\"successor-version\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generated_OpenApi_contract_snapshot_is_exported_for_tooling()
    {
        using var client = factory.CreateClient();
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
