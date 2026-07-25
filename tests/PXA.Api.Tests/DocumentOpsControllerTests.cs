using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PXA.Core.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using SkiaSharp;

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

    [Fact]
    public async Task ImportMarkdown_ReturnsDesignWithoutUnsafeLinkTarget()
    {
        using var form = new MultipartFormDataContent();
        form.Add(
            new StringContent("# Safe document\n\nOpen [this](javascript:alert%281%29).", Encoding.UTF8),
            "file",
            "sample.md");

        var response = await client.PostAsync("/api/pxa/document/import-markdown", form);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = body
            .GetProperty("pages")[0]
            .GetProperty("elements")[1]
            .GetProperty("htmlContent")
            .GetString();
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportMarkdown_RejectsUnsupportedExtension()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("# Document", Encoding.UTF8), "file", "sample.txt");

        var response = await client.PostAsync("/api/pxa/document/import-markdown", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportMarkdown_RejectsOversizedUpload()
    {
        using var form = new MultipartFormDataContent();
        form.Add(
            new ByteArrayContent(new byte[4 * 1024 * 1024 + 1]),
            "file",
            "oversized.md");

        var response = await client.PostAsync("/api/pxa/document/import-markdown", form);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task ImportMarkdown_RejectsInvalidAssetBaseUri()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("![image](asset.png)", Encoding.UTF8), "file", "sample.md");
        form.Add(new StringContent("file:///private/assets/"), "assetBaseUri");

        var response = await client.PostAsync(
            "/api/pxa/document/import-markdown",
            form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportMarkdown_UnresolvedRelativeImage_ReturnsDiagnostic()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("![image](asset.png)", Encoding.UTF8), "file", "sample.md");

        var response = await client.PostAsync(
            "/api/pxa/document/import-markdown",
            form);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var diagnostic = body.GetProperty("importDiagnostics")[0];
        Assert.Equal("PXA-MD-201", diagnostic.GetProperty("code").GetString());
        Assert.Equal("warning", diagnostic.GetProperty("severity").GetString());
        Assert.Equal("asset.png", diagnostic.GetProperty("source").GetString());
    }

    [Fact]
    public async Task MarkdownImport_ToPdf_PreservesPagesTextLinkAndImage()
    {
        var markdown = new StringBuilder()
            .AppendLine("# MarkdownIntegrationMarker")
            .AppendLine()
            .AppendLine("Open the [PXA documentation](https://example.com/docs).")
            .AppendLine()
            .AppendLine($"![pixel](data:image/png;base64,{CreatePngBase64()})")
            .AppendLine();
        for (var index = 0; index < 60; index++)
        {
            markdown
                .AppendLine($"## Section {index}")
                .AppendLine()
                .AppendLine($"Integration paragraph {index} with enough content to exercise page layout.")
                .AppendLine();
        }

        using var form = new MultipartFormDataContent();
        form.Add(
            new StringContent(markdown.ToString(), Encoding.UTF8),
            "file",
            "integration.md");

        var importResponse = await client.PostAsync(
            "/api/pxa/document/import-markdown",
            form);
        var design = await importResponse.Content.ReadFromJsonAsync<DesignExportDto>();

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.NotNull(design);
        Assert.True(design!.Pages.Count > 1);
        Assert.Contains(
            design.Pages.SelectMany(page => page.Elements),
            element => element.HtmlContent?.Contains(
                "href=\"https://example.com/docs\"",
                StringComparison.Ordinal) == true);
        Assert.Contains(
            design.Pages.SelectMany(page => page.Elements),
            element => element.Type == "image" &&
                       element.Content?.StartsWith("data:image/png;base64,", StringComparison.Ordinal) == true);

        var pdfResponse = await client.PostAsJsonAsync(
            "/api/pxa/templates/render-design",
            design);
        var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        var pdfText = Encoding.Latin1.GetString(pdfBytes);

        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.True(pdfBytes.AsSpan().StartsWith("%PDF"u8));
        Assert.Equal(
            design.Pages.Count,
            Regex.Matches(pdfText, @"/Type\s*/Page(?!s)").Count);
        Assert.Contains("MarkdownIntegrationMarker", pdfText);
        Assert.Contains("/Subtype /Image", pdfText);
        Assert.Contains("/URI (https://example.com/docs)", pdfText);
    }

    private static string CreatePngBase64()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
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
