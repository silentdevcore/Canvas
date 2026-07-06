using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Canvas.Api.Tests;

public sealed class MigrationControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public MigrationControllerTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task GetFrameworks_PxaRoute_ReturnsSupportedFrameworks()
    {
        var response = await client.GetAsync("/api/pxa/migration/frameworks");
        var frameworks = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(frameworks);
        Assert.Contains(
            frameworks!,
            framework => string.Equals(framework.GetProperty("id").GetString(), "devexpress", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(frameworks!, framework => framework.GetProperty("kind").GetString() == "pdf");
        Assert.Contains(frameworks!, framework => framework.GetProperty("kind").GetString() == "spreadsheet");
    }

    [Fact]
    public async Task Convert_PxaRoute_ReturnsCanvasCompatibleMigrationResult()
    {
        var json = JsonSerializer.Serialize(new
        {
            framework = "DevExpress",
            sourceCode = """
                using DevExpress.Pdf;

                using var processor = new PdfDocumentProcessor();
                processor.CreateEmptyDocument();
                using var graphics = processor.CreateGraphics();
                graphics.DrawString("Smoke", font, brush, 40, 40);
                processor.RenderNewPage(PdfPaperSize.A4, graphics);
                processor.SaveDocument(outputPath);
                """
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/pxa/migration/convert", content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("using Canvas.Pdf;", body.GetProperty("canvasCode").GetString());
        Assert.True(body.GetProperty("summary").GetProperty("convertedCount").GetInt32() > 0);
        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "CANMIGDEVEXP001");
    }
}
