using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SkiaSharp;

namespace Canvas.Api.Tests;

public sealed class ImageConversionControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ImageConversionControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ConvertImageToPdf_Debug_ReturnsDesignDiagnosticsAndOverlay()
    {
        using var form = CreateImageForm("HELLO 123");
        form.Add(new StringContent("eng"), "languages");
        form.Add(new StringContent("true"), "includeDiagnostics");
        form.Add(new StringContent("true"), "includeDebugOverlay");

        var response = await _client.PostAsync("/api/document/convert-image-to-pdf?debug=true", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("design", out var design));
        Assert.NotEmpty(design.GetProperty("pages")[0].GetProperty("elements").EnumerateArray());
        Assert.Equal("Tesseract", root.GetProperty("diagnostics").GetProperty("ocrEngine").GetString());
        Assert.True(root.GetProperty("diagnostics").GetProperty("wordCount").GetInt32() >= 2);
        Assert.StartsWith("data:image/png;base64,", root.GetProperty("debugOverlay").GetString());

        var text = string.Join(" ", design.GetProperty("pages")[0].GetProperty("elements")
            .EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "text")
            .Select(e => e.GetProperty("content").GetString()));

        Assert.Contains("HELLO", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("123", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertImageToPdf_Default_ReturnsPdfBytes()
    {
        using var form = CreateImageForm("PDF TEST");
        form.Add(new StringContent("eng"), "languages");

        var response = await _client.PostAsync("/api/document/convert-image-to-pdf", form);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.Latin1.GetString(bytes[..4]));
    }

    [Fact]
    public async Task ConvertImageToPdf_UnsupportedFormat_Returns415()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "scan.txt");

        var response = await _client.PostAsync("/api/document/convert-image-to-pdf", form);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private static MultipartFormDataContent CreateImageForm(string text)
    {
        var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(MakeTextImage(text));
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(image, "file", "scan.png");
        return form;
    }

    private static byte[] MakeTextImage(string text)
    {
        using var bitmap = new SKBitmap(720, 220, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 72);
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
            };
            canvas.DrawText(text, 48, 132, font, paint);
        }

        using var skImage = SKImage.FromBitmap(bitmap);
        using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
