using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PXA.Api.Tests;

public sealed class SystemControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public SystemControllerTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/system/brand")]
    [InlineData("/api/pxa/system/brand")]
    public async Task GetBranding_ReturnsPxaBrandingAndLegacyCompatibility(string route)
    {
        var response = await client.GetAsync(route);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Power Dox Automation", body.GetProperty("productName").GetString());
        Assert.Equal("PXA", body.GetProperty("developerName").GetString());
        Assert.Equal("pxa", body.GetProperty("cliName").GetString());
        Assert.Equal(".pxa", body.GetProperty("nativeFileExtension").GetString());
        Assert.Equal("PXA", body.GetProperty("legacyProductName").GetString());
        Assert.Contains(
            body.GetProperty("compatibilityNotes").EnumerateArray(),
            note => note.GetString() == "Legacy /api routes remain compatible.");
    }
}
