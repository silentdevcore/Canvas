using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Canvas.Api.Tests;

public sealed class PdfViewerAnnotationsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PdfViewerAnnotationsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SaveAndGetAnnotations_RoundTripsSidecar()
    {
        var documentId = $"doc-{Guid.NewGuid():N}";
        var request = new
        {
            documentId,
            version = 1,
            sourceName = "review.pdf",
            exportedAt = DateTimeOffset.Parse("2026-06-25T10:00:00Z"),
            annotations = new object[]
            {
                new
                {
                    id = "a1",
                    type = "highlight",
                    pageNumber = 1,
                    xPct = 10,
                    yPct = 20,
                    widthPct = 30,
                    heightPct = 5,
                    text = "",
                    author = "Reviewer",
                    createdAt = "2026-06-25T10:00:00Z",
                    color = "#fef08a",
                    locked = false,
                },
                new
                {
                    id = "ink1",
                    type = "ink",
                    pageNumber = 2,
                    xPct = 0,
                    yPct = 0,
                    widthPct = 100,
                    heightPct = 100,
                    text = "",
                    author = "Reviewer",
                    createdAt = "2026-06-25T10:00:01Z",
                    color = "#ef4444",
                    locked = true,
                    points = new[] { new { xPct = 10, yPct = 10 }, new { xPct = 20, yPct = 22 } },
                },
            },
        };

        var saveResponse = await _client.PostAsJsonAsync("/api/pdf-viewer/annotations", request);
        var getResponse = await _client.GetAsync($"/api/pdf-viewer/annotations/{documentId}");

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var json = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(documentId, root.GetProperty("documentId").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("review.pdf", root.GetProperty("sourceName").GetString());
        Assert.Equal(2, root.GetProperty("annotationCount").GetInt32());
        Assert.Equal("ink", root.GetProperty("annotations")[1].GetProperty("type").GetString());
        Assert.True(root.GetProperty("annotations")[1].GetProperty("locked").GetBoolean());
    }

    [Fact]
    public async Task SaveAnnotations_RejectsMissingDocumentId()
    {
        var response = await _client.PostAsJsonAsync("/api/pdf-viewer/annotations", new
        {
            version = 1,
            annotations = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveAnnotations_RejectsUnsupportedVersion()
    {
        var response = await _client.PostAsJsonAsync("/api/pdf-viewer/annotations", new
        {
            documentId = "doc-version",
            version = 2,
            annotations = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAnnotations_RemovesStoredSidecar()
    {
        var documentId = $"doc-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/pdf-viewer/annotations", new
        {
            documentId,
            version = 1,
            annotations = Array.Empty<object>(),
        });

        var deleteResponse = await _client.DeleteAsync($"/api/pdf-viewer/annotations/{documentId}");
        var getResponse = await _client.GetAsync($"/api/pdf-viewer/annotations/{documentId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
