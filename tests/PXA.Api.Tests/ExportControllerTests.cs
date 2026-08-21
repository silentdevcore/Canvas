using System.Net;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PXA.Api.Tests;

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

    [Fact]
    public async Task Export_AllRegisteredFormats_ReturnMatchingMediaType_FileName_AndSignature()
    {
        var expectations = new Dictionary<string, (string MediaType, string Extension, byte[]? Signature)>
        {
            ["html"] = ("text/html", ".html", null),
            ["xml"] = ("application/xml", ".xml", null),
            ["svg"] = ("image/svg+xml", ".svg", null),
            ["csv"] = ("text/csv", ".csv", null),
            ["md"] = ("text/markdown", ".md", null),
            ["png"] = ("image/png", ".png", [0x89, 0x50, 0x4E, 0x47]),
            ["jpeg"] = ("image/jpeg", ".jpg", [0xFF, 0xD8, 0xFF]),
            ["tiff"] = ("image/tiff", ".tiff", [0x49, 0x49, 0x2A, 0x00]),
            ["odt"] = ("application/vnd.oasis.opendocument.text", ".odt", [0x50, 0x4B]),
            ["word"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx", [0x50, 0x4B]),
            ["excel"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx", [0x50, 0x4B]),
        };

        foreach (var (format, expected) in expectations)
        {
            var response = await PostExport(format);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expected.MediaType, response.Content.Headers.ContentType?.MediaType);
            Assert.EndsWith(expected.Extension, response.Content.Headers.ContentDisposition?.FileNameStar ??
                response.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(bytes);
            if (expected.Signature is not null)
                Assert.True(bytes.AsSpan().StartsWith(expected.Signature), $"{format} bytes do not match the declared format.");
        }
    }

    [Theory]
    [InlineData("png", ".png", "89504E47")]
    [InlineData("jpeg", ".jpg", "FFD8FF")]
    [InlineData("tiff", ".tiff", "49492A00")]
    [InlineData("svg", ".svg", "3C")]
    public async Task Export_MultiPageImages_ReturnZipWithOneCorrectFilePerPage(
        string format, string entryExtension, string signatureHex)
    {
        var design = new DesignExportDto
        {
            Id = SampleDesign.Id,
            Name = "Multi Page",
            PageSettings = SampleDesign.PageSettings,
            Pages = [SampleDesign.Pages[0], new PageDto { Id = "p2", Elements = SampleDesign.Pages[0].Elements }],
        };

        var response = await PostExport(format, design: design);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith($"-{format}-pages.zip", response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName?.Trim('"'), StringComparison.OrdinalIgnoreCase);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Equal(2, archive.Entries.Count);
        var expectedSignature = Convert.FromHexString(signatureHex);
        foreach (var entry in archive.Entries)
        {
            Assert.EndsWith(entryExtension, entry.Name, StringComparison.OrdinalIgnoreCase);
            using var stream = entry.Open();
            var prefix = new byte[Math.Max(256, expectedSignature.Length)];
            var read = await stream.ReadAsync(prefix);
            Assert.True(read >= expectedSignature.Length);
            if (format == "svg")
                Assert.Contains("<svg", Encoding.UTF8.GetString(prefix, 0, read), StringComparison.OrdinalIgnoreCase);
            else
                Assert.True(prefix.AsSpan(0, read).StartsWith(expectedSignature));
        }
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
    public async Task GetFormats_PxaRoute_Returns200_WithAllFormats()
    {
        var response = await _client.GetAsync("/api/pxa/export/formats");
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

    [Fact]
    public async Task Export_PxaRoute_Html_Returns200()
    {
        var response = await PostExport("html", "/api/pxa/export");
        var body     = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hello", body);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PostExport(
        string format,
        string route = "/api/export",
        DesignExportDto? design = null)
    {
        var json    = JsonSerializer.Serialize(design ?? SampleDesign);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync($"{route}?format={format}", content);
    }
}
