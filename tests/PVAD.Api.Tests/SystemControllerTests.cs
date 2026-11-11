using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PVAD.Api.Tests;

public sealed class SystemControllerTests : IClassFixture<PvadApiTestFactory>
{
    private readonly HttpClient client;

    public SystemControllerTests(PvadApiTestFactory factory)
    {
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/system/brand")]
    [InlineData("/api/pvad/system/brand")]
    [InlineData("/api/pvad/system/brand")]
    public async Task GetBranding_ReturnsPvadBranding(string route)
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
        Assert.False(body.TryGetProperty("legacyProductName", out _));
        Assert.False(body.TryGetProperty("legacyNamespacePrefix", out _));
        Assert.Contains(
            body.GetProperty("compatibilityNotes").EnumerateArray(),
            note => note.GetString() == "Unprefixed /api routes remain available.");
    }
}
