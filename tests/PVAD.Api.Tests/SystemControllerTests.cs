using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PVAD.Api.Tests;

public sealed class SystemControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public SystemControllerTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/system/brand")]
    [InlineData("/api/pvad/system/brand")]
    [InlineData("/api/pxa/system/brand")]
    public async Task GetBranding_ReturnsPvadBrandingAndLegacyCompatibility(string route)
    {
        var response = await client.GetAsync(route);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("PVADE", body.GetProperty("productName").GetString());
        Assert.Equal("PVAD", body.GetProperty("developerName").GetString());
        Assert.Equal("pvad", body.GetProperty("cliName").GetString());
        Assert.Equal(".pvadp", body.GetProperty("nativeFileExtension").GetString());
        Assert.Equal(new[] { ".pvadp", ".pvads" },
            body.GetProperty("nativeFileExtensions").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("PXA", body.GetProperty("legacyProductName").GetString());
        Assert.Equal("PXA", body.GetProperty("legacyNamespacePrefix").GetString());
        Assert.Contains(
            body.GetProperty("compatibilityNotes").EnumerateArray(),
            note => note.GetString() == "Legacy /api routes remain compatible.");
    }
}
