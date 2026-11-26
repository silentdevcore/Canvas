using System.Net;
using System.Text;
using System.Text.Json;
using PVAD.Licensing;
using PVAD.Testing;

namespace PVAD.Api.Tests;

[CollectionDefinition("Native spreadsheet license renewal", DisableParallelization = true)]
public sealed class NativeSpreadsheetLicenseCollection { }

// This collection runs without concurrent collections because the native license
// is process-wide. Both replacement and restoration use normal signed files.
[Collection("Native spreadsheet license renewal")]
public sealed class PvadNativeSpreadsheetLicenseTests
{
    [Fact]
    public async Task Actual_spreadsheet_routes_reject_pdf_only_grants_and_recover_after_renewal()
    {
        var integrated = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Run with a signed native test session.");
        using var factory = new PvadApiTestFactory();
        using var client = factory.CreateCompanyProductClient();
        var workbook = new SpreadsheetDto
        {
            Name = "Native license test", Sheets = [new SheetDto
            {
                Id = "sheet-1", Name = "Data",
                Cells = [new CellDto { Row = 0, Col = 0, Type = "text", Value = "{{amount}}" }],
            }],
        };
        var before = JsonSerializer.Serialize(workbook);
        try
        {
            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "pdf.json"));
            foreach (var format in new[] { "xlsx", "csv", "tsv" })
            {
                // Empty CSV/TSV exports must not bypass the native entry check.
                using var response = await client.PostAsJsonAsync($"/api/pvad/spreadsheet/export?format={format}", new SpreadsheetDto());
                await Denied(response);
                using var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("A,3")), "file", "synthetic." + format);
                using var imported = await client.PostAsync("/api/pvad/spreadsheet/import", form);
                await Denied(imported);
            }

            foreach (var operation in new[] { "calculate", "to-design", "sort?range=A1:A1", "find-replace?find=amount", "render?format=pdf" })
            {
                using var response = await client.PostAsJsonAsync("/api/pvad/spreadsheet/" + operation, workbook);
                await Denied(response);
            }
            using (var response = await client.PostAsJsonAsync("/api/pvad/spreadsheet/fill", new { workbook, data = new { amount = 42 } }))
                await Denied(response);
            using (var response = await client.PostAsJsonAsync("/api/pvad/spreadsheet/from-data", new[] { new { amount = 42 } }))
                await Denied(response);
            using (var response = await client.PostAsJsonAsync("/api/pvad/spreadsheet/validate", workbook))
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(before, JsonSerializer.Serialize(workbook));
        }
        finally { PvadLicense.LoadFromFile(integrated); }

        using var recovered = await client.PostAsJsonAsync("/api/pvad/spreadsheet/export", workbook);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.True((await recovered.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("PK"u8));
    }

    private static async Task Denied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Content.Headers.ContentDisposition);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PVADLIC012", document.RootElement.GetProperty("code").GetString());
    }
}
