using System.Net;
using System.Text.Json;
using Canvas.Pdf;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Canvas.Api.Tests;

public sealed class PdfViewerFormsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PdfViewerFormsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ExtractForms_ReturnsAcroFormFields()
    {
        var inputPdf = CreateSampleFormPdf();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(inputPdf)
        {
            Headers =
            {
                ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"),
            },
        }, "file", "form.pdf");

        var response = await _client.PostAsync("/api/pdf-viewer/forms/extract", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("form.pdf", root.GetProperty("sourceName").GetString());

        var fields = root.GetProperty("fields");
        Assert.Equal(4, fields.GetArrayLength());
        Assert.Contains(fields.EnumerateArray(), field =>
            field.GetProperty("name").GetString() == "customer.name" &&
            field.GetProperty("kind").GetString() == "text" &&
            field.GetProperty("value").GetString() == "Ada" &&
            !field.GetProperty("multiline").GetBoolean());
        Assert.Contains(fields.EnumerateArray(), field =>
            field.GetProperty("name").GetString() == "customer.notes" &&
            field.GetProperty("kind").GetString() == "text" &&
            field.GetProperty("value").GetString() == "Initial note" &&
            field.GetProperty("multiline").GetBoolean());
        Assert.Contains(fields.EnumerateArray(), field =>
            field.GetProperty("name").GetString() == "approval.accepted" &&
            field.GetProperty("kind").GetString() == "checkbox" &&
            field.GetProperty("value").GetBoolean());
        Assert.Contains(fields.EnumerateArray(), field =>
            field.GetProperty("name").GetString() == "priority" &&
            field.GetProperty("kind").GetString() == "dropdown" &&
            field.GetProperty("value").GetString() == "Normal" &&
            field.GetProperty("options").EnumerateArray().Select(option => option.GetString()).SequenceEqual(["Low", "Normal", "High"]));
    }

    [Fact]
    public async Task FillForms_ReturnsPdfWithUpdatedValues()
    {
        var inputPdf = CreateSampleFormPdf();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(inputPdf)
        {
            Headers =
            {
                ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"),
            },
        }, "file", "form.pdf");
        form.Add(new StringContent("""
            {
              "fields": [
                { "name": "customer.name", "value": "Grace" },
                { "name": "customer.notes", "value": "Updated note" },
                { "name": "approval.accepted", "value": false },
                { "name": "priority", "value": "High" }
              ]
            }
            """), "fields");

        var fillResponse = await _client.PostAsync("/api/pdf-viewer/forms/fill", form);

        Assert.Equal(HttpStatusCode.OK, fillResponse.StatusCode);
        Assert.Equal("application/pdf", fillResponse.Content.Headers.ContentType?.MediaType);

        using var extractForm = new MultipartFormDataContent();
        extractForm.Add(new ByteArrayContent(await fillResponse.Content.ReadAsByteArrayAsync())
        {
            Headers =
            {
                ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"),
            },
        }, "file", "form-filled.pdf");

        var extractResponse = await _client.PostAsync("/api/pdf-viewer/forms/extract", extractForm);
        using var json = JsonDocument.Parse(await extractResponse.Content.ReadAsStringAsync());
        var fields = json.RootElement.GetProperty("fields");

        Assert.Equal(HttpStatusCode.OK, extractResponse.StatusCode);
        Assert.Contains(fields.EnumerateArray(), field =>
            field.GetProperty("name").GetString() == "customer.name" &&
            field.GetProperty("value").GetString() == "Grace");
        Assert.Contains(fields.EnumerateArray(), field =>
            field.GetProperty("name").GetString() == "customer.notes" &&
            field.GetProperty("value").GetString() == "Updated note");
        Assert.Contains(fields.EnumerateArray(), field =>
            field.GetProperty("name").GetString() == "approval.accepted" &&
            !field.GetProperty("value").GetBoolean());
        Assert.Contains(fields.EnumerateArray(), field =>
            field.GetProperty("name").GetString() == "priority" &&
            field.GetProperty("value").GetString() == "High");
    }

    private static byte[] CreateSampleFormPdf()
    {
        var document = new PdfDocument();
        var page = document.AddPage(300, 220);
        page.DrawTextFromTop("Form PDF", 24, 24, 12);
        page.AddTextField("customer.name", 24, 150, 120, 18, "Ada");
        page.AddMultilineTextField("customer.notes", 24, 105, 150, 36, "Initial note");
        page.AddCheckBox("approval.accepted", 24, 75, 14, isChecked: true);
        page.AddComboBox("priority", 24, 45, 100, 18, ["Low", "Normal", "High"], "Normal");

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }
}
