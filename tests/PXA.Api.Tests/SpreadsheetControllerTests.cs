using System.Net;
using System.Text;
using System.Text.Json;
using PXA.Core.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PXA.Api.Tests;

public sealed class SpreadsheetControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public SpreadsheetControllerTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task FromData_PxaRoute_ReturnsWorkbook()
    {
        var json = """
            [
              { "Name": "North", "Amount": 42 },
              { "Name": "South", "Amount": 17 }
            ]
            """;
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/pxa/spreadsheet/from-data?sheetName=Sales", content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sales", body.GetProperty("name").GetString());
        Assert.Equal("Sales", body.GetProperty("sheets")[0].GetProperty("name").GetString());
        Assert.NotEmpty(body.GetProperty("sheets")[0].GetProperty("cells").EnumerateArray());
    }

    [Fact]
    public async Task Validate_PxaRoute_ReturnsValidationResult()
    {
        var json = JsonSerializer.Serialize(SampleWorkbook());
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/pxa/spreadsheet/validate", content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("valid").GetBoolean());
        Assert.Equal("1.0", body.GetProperty("version").GetString());
        Assert.Equal("1.0", body.GetProperty("supportedVersion").GetString());
    }

    private static SpreadsheetDto SampleWorkbook() => new()
    {
        Id = "workbook-1",
        Name = "Budget",
        Sheets =
        [
            new SheetDto
            {
                Id = "sheet-1",
                Name = "Sheet1",
                Cells =
                [
                    new CellDto { Row = 0, Col = 0, Type = "text", Value = "Name" },
                    new CellDto { Row = 1, Col = 0, Type = "text", Value = "North" },
                    new CellDto { Row = 1, Col = 1, Type = "number", Value = 42 }
                ]
            }
        ]
    };
}
