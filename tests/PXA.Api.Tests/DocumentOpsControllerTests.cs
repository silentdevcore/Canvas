using System.Net;
using System.Text;
using System.Text.Json;
using PXA.Core.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PXA.Api.Tests;

public sealed class DocumentOpsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public DocumentOpsControllerTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task FindReplace_PxaRoute_ReturnsUpdatedDesignAndCount()
    {
        var json = JsonSerializer.Serialize(new
        {
            design = SampleDesign(),
            find = "Hello",
            replace = "PXA"
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/pxa/document/find-replace", content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("replacementCount").GetInt32() > 0);
        var firstElement = body.GetProperty("design").GetProperty("pages")[0].GetProperty("elements")[0];
        Assert.Equal("PXA World", firstElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Clone_PxaRoute_ReturnsRenamedDesign()
    {
        var json = JsonSerializer.Serialize(new
        {
            design = SampleDesign(),
            newName = "PXA Clone"
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/pxa/document/clone", content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("PXA Clone", body.GetProperty("name").GetString());
        Assert.NotEqual("design-1", body.GetProperty("id").GetString());
    }

    [Fact]
    public async Task ConvertImageToPdf_PxaRoute_UnsupportedFormatReturns415()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "scan.txt");

        var response = await client.PostAsync("/api/pxa/document/convert-image-to-pdf", form);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private static DesignExportDto SampleDesign() => new()
    {
        Id = "design-1",
        Name = "Sample",
        Pages =
        [
            new PageDto
            {
                Id = "page-1",
                Elements =
                [
                    new ElementDto
                    {
                        Id = "text-1",
                        Type = "text",
                        X = 10,
                        Y = 10,
                        Width = 200,
                        Height = 40,
                        Content = "Hello World"
                    }
                ]
            }
        ],
        PageSettings = new PageSettingsDto { Width = 595, Height = 842 }
    };
}
