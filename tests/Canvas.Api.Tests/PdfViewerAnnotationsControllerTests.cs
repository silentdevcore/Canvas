using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Canvas.Importer;
using Canvas.Pdf;
using Canvas.WebApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Canvas.Api.Tests;

public sealed class PdfViewerAnnotationsControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _storageRoot;

    public PdfViewerAnnotationsControllerTests(WebApplicationFactory<Program> factory)
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), "canvas-pdf-viewer-api-tests", Guid.NewGuid().ToString("N"));
        _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["PdfViewer:AnnotationStoragePath"] = _storageRoot,
                    });
                });
            })
            .CreateClient();
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
    public async Task SaveAndGetAnnotations_PxaRoute_RoundTripsSidecar()
    {
        var documentId = $"pxa-doc-{Guid.NewGuid():N}";
        var request = new
        {
            documentId,
            version = 1,
            sourceName = "pxa-review.pdf",
            annotations = new object[]
            {
                new
                {
                    id = "note1",
                    type = "note",
                    pageNumber = 1,
                    xPct = 10,
                    yPct = 10,
                    widthPct = 20,
                    heightPct = 8,
                    text = "PXA note",
                    author = "Reviewer",
                    createdAt = "2026-06-25T10:00:00Z",
                    color = "#facc15",
                    locked = false,
                },
            },
        };

        var saveResponse = await _client.PostAsJsonAsync("/api/pxa/pdf-viewer/annotations", request);
        var getResponse = await _client.GetAsync($"/api/pxa/pdf-viewer/annotations/{documentId}");

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var json = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(documentId, root.GetProperty("documentId").GetString());
        Assert.Equal("pxa-review.pdf", root.GetProperty("sourceName").GetString());
        Assert.Equal(1, root.GetProperty("annotationCount").GetInt32());
        Assert.Equal("PXA note", root.GetProperty("annotations")[0].GetProperty("text").GetString());
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

    [Fact]
    public void AnnotationStore_ReloadsSavedSidecarFromDisk()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "canvas-pdf-viewer-tests", Guid.NewGuid().ToString("N"));
        var documentId = $"doc/{Guid.NewGuid():N}";

        try
        {
            using var annotations = JsonDocument.Parse("""[{ "id": "note1", "type": "note", "pageNumber": 1 }]""");
            var firstStore = new PdfViewerAnnotationStore(storageRoot);
            firstStore.Save(
                documentId,
                version: 1,
                sourceName: "review.pdf",
                exportedAt: DateTimeOffset.Parse("2026-06-25T10:00:00Z"),
                annotations: annotations.RootElement);

            var secondStore = new PdfViewerAnnotationStore(storageRoot);
            var found = secondStore.TryGet(documentId, out var stored);

            Assert.True(found);
            Assert.Equal(documentId, stored.DocumentId);
            Assert.Equal("review.pdf", stored.SourceName);
            Assert.Equal("note", stored.Annotations[0].GetProperty("type").GetString());
        }
        finally
        {
            if (Directory.Exists(storageRoot))
                Directory.Delete(storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task FlattenAnnotations_ReturnsReviewedPdf()
    {
        var inputPdf = CreateSamplePdf();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(inputPdf)
        {
            Headers =
            {
                ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"),
            },
        }, "file", "review.pdf");
        form.Add(new StringContent("""
            {
              "version": 1,
              "sourceName": "review.pdf",
              "exportedAt": "2026-06-25T10:00:00Z",
              "annotations": [
                {
                  "id": "note1",
                  "type": "note",
                  "pageNumber": 1,
                  "xPct": 10,
                  "yPct": 10,
                  "widthPct": 30,
                  "heightPct": 12,
                  "text": "Reviewed",
                  "author": "Reviewer",
                  "createdAt": "2026-06-25T10:00:00Z",
                  "color": "#facc15"
                }
              ]
            }
            """), "sidecar");

        var response = await _client.PostAsync("/api/pdf-viewer/annotations/flatten", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        await using var output = await response.Content.ReadAsStreamAsync();
        var document = await new PdfImporter().LoadAsync(output);
        Assert.Contains(document.Pages.SelectMany(page => page.TextObjects), text => text.Text == "Reviewed");
    }

    [Fact]
    public async Task EmbedAnnotations_ReturnsPdfWithNativeAnnotations()
    {
        var inputPdf = CreateSamplePdf();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(inputPdf)
        {
            Headers =
            {
                ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"),
            },
        }, "file", "review.pdf");
        form.Add(new StringContent("""
            {
              "version": 1,
              "sourceName": "review.pdf",
              "exportedAt": "2026-06-25T10:00:00Z",
              "annotations": [
                {
                  "id": "note1",
                  "type": "note",
                  "pageNumber": 1,
                  "xPct": 10,
                  "yPct": 10,
                  "widthPct": 10,
                  "heightPct": 10,
                  "text": "Native note",
                  "author": "Reviewer",
                  "createdAt": "2026-06-25T10:00:00Z",
                  "color": "#facc15"
                },
                {
                  "id": "text1",
                  "type": "freeText",
                  "pageNumber": 1,
                  "xPct": 20,
                  "yPct": 20,
                  "widthPct": 30,
                  "heightPct": 10,
                  "text": "Native free text",
                  "author": "Reviewer",
                  "createdAt": "2026-06-25T10:00:00Z",
                  "color": "#2563eb"
                },
                {
                  "id": "highlight1",
                  "type": "highlight",
                  "pageNumber": 1,
                  "xPct": 15,
                  "yPct": 40,
                  "widthPct": 45,
                  "heightPct": 8,
                  "text": "Native highlight",
                  "author": "Reviewer",
                  "createdAt": "2026-06-25T10:00:00Z",
                  "color": "#fef08a",
                  "quadPoints": [
                    {
                      "x1Pct": 10,
                      "y1Pct": 20,
                      "x2Pct": 40,
                      "y2Pct": 20,
                      "x3Pct": 10,
                      "y3Pct": 26,
                      "x4Pct": 40,
                      "y4Pct": 26
                    }
                  ]
                }
              ]
            }
            """), "sidecar");

        var response = await _client.PostAsync("/api/pdf-viewer/annotations/embed", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var content = System.Text.Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("/Annots", content, StringComparison.Ordinal);
        Assert.Contains("/Subtype /Text", content, StringComparison.Ordinal);
        Assert.Contains("/Subtype /FreeText", content, StringComparison.Ordinal);
        Assert.Contains("/Subtype /Highlight", content, StringComparison.Ordinal);
        Assert.Contains("/AP << /N", content, StringComparison.Ordinal);
        Assert.Contains("/Subtype /Form", content, StringComparison.Ordinal);
        Assert.Contains("/QuadPoints [30 144 120 144 30 133.2 120 133.2]", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAnnotations_ReturnsNativeAnnotationsAsSidecar()
    {
        var inputPdf = CreateAnnotatedPdf();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(inputPdf)
        {
            Headers =
            {
                ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"),
            },
        }, "file", "annotated.pdf");

        var response = await _client.PostAsync("/api/pdf-viewer/annotations/extract", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("annotated.pdf", root.GetProperty("sourceName").GetString());

        var annotations = root.GetProperty("annotations");
        Assert.Equal(3, annotations.GetArrayLength());
        Assert.Equal("note", annotations[0].GetProperty("type").GetString());
        Assert.Equal("Native note", annotations[0].GetProperty("text").GetString());
        Assert.Equal(10, annotations[0].GetProperty("xPct").GetDouble());
        Assert.Equal(20, annotations[0].GetProperty("yPct").GetDouble());
        Assert.Equal("freeText", annotations[1].GetProperty("type").GetString());
        Assert.Equal("highlight", annotations[2].GetProperty("type").GetString());
        Assert.Equal(10, annotations[2].GetProperty("xPct").GetDouble());
        Assert.Equal(14.4444, annotations[2].GetProperty("yPct").GetDouble(), precision: 4);
        Assert.Equal(46.6667, annotations[2].GetProperty("widthPct").GetDouble(), precision: 4);
        Assert.Equal(18.8889, annotations[2].GetProperty("heightPct").GetDouble(), precision: 4);

        var quadPoints = annotations[2].GetProperty("quadPoints");
        Assert.Equal(2, quadPoints.GetArrayLength());
        Assert.Equal(10, quadPoints[0].GetProperty("x1Pct").GetDouble());
        Assert.Equal(14.4444, quadPoints[0].GetProperty("y1Pct").GetDouble(), precision: 4);
        Assert.Equal(36.6667, quadPoints[0].GetProperty("x2Pct").GetDouble(), precision: 4);
        Assert.Equal(21.1111, quadPoints[0].GetProperty("y3Pct").GetDouble(), precision: 4);
        Assert.Equal(56.6667, quadPoints[1].GetProperty("x2Pct").GetDouble(), precision: 4);
        Assert.Equal(33.3333, quadPoints[1].GetProperty("y3Pct").GetDouble(), precision: 4);
    }

    [Fact]
    public async Task RedactAnnotations_RemovesCoveredText()
    {
        var inputPdf = CreateSamplePdf();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(inputPdf)
        {
            Headers =
            {
                ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"),
            },
        }, "file", "review.pdf");
        form.Add(new StringContent("""
            {
              "version": 1,
              "sourceName": "review.pdf",
              "exportedAt": "2026-06-25T10:00:00Z",
              "annotations": [
                {
                  "id": "redaction1",
                  "type": "redaction",
                  "pageNumber": 1,
                  "xPct": 0,
                  "yPct": 0,
                  "widthPct": 100,
                  "heightPct": 100,
                  "text": "Redaction mark",
                  "reason": "Privacy request",
                  "author": "Reviewer",
                  "createdAt": "2026-06-25T10:00:00Z",
                  "color": "#111827"
                }
              ]
            }
            """), "sidecar");

        var response = await _client.PostAsync("/api/pdf-viewer/annotations/redact", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var content = System.Text.Encoding.ASCII.GetString(bytes);
        Assert.Contains("/RedactionAudit", content, StringComparison.Ordinal);
        Assert.Contains("Privacy request", content, StringComparison.Ordinal);
        Assert.Contains("Reviewer", content, StringComparison.Ordinal);
        Assert.Contains("2026-06-25T10:00:00", content, StringComparison.Ordinal);

        await using var output = new MemoryStream(bytes);
        var document = await new PdfImporter().LoadAsync(output);
        Assert.DoesNotContain(document.Pages.SelectMany(page => page.TextObjects), text => text.Text == "Source PDF");
    }

    public void Dispose()
    {
        _client.Dispose();

        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }

    private static byte[] CreateSamplePdf()
    {
        var document = new PdfDocument();
        var page = document.AddPage(300, 180);
        page.DrawTextFromTop("Source PDF", 24, 24, 12);

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static byte[] CreateAnnotatedPdf()
    {
        var document = new PdfDocument();
        var page = document.AddPage(300, 180);
        page.DrawTextFromTop("Annotated PDF", 24, 24, 12);
        page.AddStickyNoteAnnotation(30, 126, 30, 18, "Native note", PdfColor.FromRgb(250, 204, 21));
        page.AddFreeTextAnnotation(75, 90, 90, 18, "Native free text", PdfColor.FromRgb(37, 99, 235));
        page.AddHighlightAnnotation(
            30,
            120,
            140,
            34,
            "Native highlight",
            PdfColor.FromRgb(254, 240, 138),
            0.45,
            [
                new PdfMarkupQuadPoint(30, 154, 110, 154, 30, 142, 110, 142),
                new PdfMarkupQuadPoint(30, 136, 170, 136, 30, 120, 170, 120),
            ]);

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }
}
