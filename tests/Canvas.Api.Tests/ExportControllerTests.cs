using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Canvas.Api.Tests;

public sealed class ExportControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ExportControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static readonly DesignExportDto SampleDesign = new()
    {
        Id   = "test-id",
        Name = "Integration Test",
        Pages =
        [
            new PageDto
            {
                Id = "p1",
                Elements =
                [
                    new ElementDto { Id = "e1", Type = "text", X = 0, Y = 0, Width = 200, Height = 30, Content = "Hello" },
                    new ElementDto
                    {
                        Id = "e2", Type = "table", X = 0, Y = 40, Width = 300, Height = 100,
                        CellData = [["A", "B"], ["1", "2"]],
                    },
                ],
            },
        ],
        PageSettings = new PageSettingsDto { Width = 595, Height = 842 },
    };

    // ─── POST /api/export?format=html ─────────────────────────────────────────

    [Fact]
    public async Task Export_Html_Returns200_WithCorrectContentType()
    {
        var response = await PostExport("html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Export_Html_ResponseContainsDesignContent()
    {
        var response = await PostExport("html");
        var body     = await response.Content.ReadAsStringAsync();

        Assert.Contains("Hello", body);
        Assert.Contains("<!DOCTYPE html>", body);
    }

    // ─── POST /api/export?format=xml ──────────────────────────────────────────

    [Fact]
    public async Task Export_Xml_Returns200_WithCorrectContentType()
    {
        var response = await PostExport("xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/xml", response.Content.Headers.ContentType?.MediaType);
    }

    // ─── POST /api/export?format=csv ──────────────────────────────────────────

    [Fact]
    public async Task Export_Csv_Returns200_WithCorrectContentType()
    {
        var response = await PostExport("csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    // ─── POST /api/export?format=md ───────────────────────────────────────────

    [Fact]
    public async Task Export_Markdown_Returns200_WithCorrectContentType()
    {
        var response = await PostExport("md");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/markdown", response.Content.Headers.ContentType?.MediaType);
    }

    // ─── POST /api/export?format=word ─────────────────────────────────────────

    [Fact]
    public async Task Export_Word_Returns200_WithDocxSignature()
    {
        var response = await PostExport("word");
        var bytes    = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B, "DOCX should start with PK magic bytes");
    }

    // ─── POST /api/export?format=excel ────────────────────────────────────────

    [Fact]
    public async Task Export_Excel_Returns200_WithXlsxSignature()
    {
        var response = await PostExport("excel");
        var bytes    = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B, "XLSX should start with PK magic bytes");
    }

    // ─── POST /api/export?format=png ──────────────────────────────────────────

    [Fact]
    public async Task Export_Png_Returns200_WithPngSignature()
    {
        var response = await PostExport("png");
        var bytes    = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(bytes.Length >= 2 && bytes[0] == 0x89 && bytes[1] == 0x50, "PNG should start with PNG magic bytes");
    }

    // ─── Unknown format → 415 ────────────────────────────────────────────────

    [Fact]
    public async Task Export_UnknownFormat_Returns415()
    {
        var response = await PostExport("pdf2");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Export_UnknownFormat_ResponseListsSupportedFormats()
    {
        var response = await PostExport("pdf2");
        var body     = await response.Content.ReadAsStringAsync();

        Assert.Contains("html", body);
        Assert.Contains("csv",  body);
    }

    // ─── Missing format param → 400 ───────────────────────────────────────────

    [Fact]
    public async Task Export_MissingFormat_Returns400()
    {
        var json     = JsonSerializer.Serialize(SampleDesign);
        var content  = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/export", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── GET /api/export/formats ──────────────────────────────────────────────

    [Fact]
    public async Task GetFormats_Returns200_WithAllFormats()
    {
        var response = await _client.GetAsync("/api/export/formats");
        var formats  = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(formats);
        Assert.Equal(11, formats!.Length);
    }

    [Fact]
    public async Task GetFormats_IncludesCapabilityFields()
    {
        var response = await _client.GetAsync("/api/export/formats");
        var formats  = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(formats);
        var html = formats!.First(f => f.GetProperty("key").GetString() == "html");
        Assert.True(html.GetProperty("supportsMultiPage").GetBoolean());
        Assert.True(html.GetProperty("supportsImages").GetBoolean());
        Assert.True(html.GetProperty("supportsRichText").GetBoolean());
        Assert.True(html.GetProperty("supportsFormFields").GetBoolean());
    }

    [Fact]
    public async Task GetFormats_CsvHasCorrectCapabilities()
    {
        var response = await _client.GetAsync("/api/export/formats");
        var formats  = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(formats);
        var csv = formats!.First(f => f.GetProperty("key").GetString() == "csv");
        Assert.True(csv.GetProperty("supportsMultiPage").GetBoolean());
        Assert.False(csv.GetProperty("supportsImages").GetBoolean());
        Assert.False(csv.GetProperty("supportsRichText").GetBoolean());
        Assert.False(csv.GetProperty("supportsFormFields").GetBoolean());
    }

    // ─── Case-insensitive format key ──────────────────────────────────────────

    [Fact]
    public async Task Export_FormatKey_IsCaseInsensitive()
    {
        var response = await PostExport("HTML");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PostExport(string format)
    {
        var json    = JsonSerializer.Serialize(SampleDesign);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync($"/api/export?format={format}", content);
    }
}
