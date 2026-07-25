using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PXA.Api.Tests;

public sealed class TemplatesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public TemplatesControllerTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTemplateNames_PxaRoute_RequiresAuthentication()
    {
        var response = await client.GetAsync("/api/pxa/templates");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidateTemplate_PxaRoute_ReturnsValidResult()
    {
        var json = JsonSerializer.Serialize(new
        {
            id = "template-1",
            name = "PXA Template",
            elements = new[]
            {
                new
                {
                    id = "text-1",
                    type = 0,
                    props = new { text = "Hello" },
                    x = 10,
                    y = 10,
                    width = 100,
                    height = 20
                }
            },
            pageSettings = new
            {
                width = 595,
                height = 842,
                orientation = "portrait",
                margins = new { top = 72, right = 72, bottom = 72, left = 72 }
            }
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/pxa/templates/validate", content);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(result.GetProperty("isValid").GetBoolean());
        Assert.Empty(result.GetProperty("errors").EnumerateArray());
    }
}
