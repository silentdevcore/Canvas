using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PXA.Api.Tests;

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
        Assert.Contains(
            frameworks!,
            framework =>
                framework.GetProperty("domain").GetString() == "pdf" &&
                framework.GetProperty("migrationKind").GetString() == "code" &&
                framework.GetProperty("provider").GetString() == "DevExpress");
        Assert.Contains(
            frameworks!,
            framework =>
                framework.GetProperty("domain").GetString() == "spreadsheet" &&
                framework.GetProperty("migrationKind").GetString() == "code" &&
                framework.GetProperty("provider").GetString() == "Aspose");
    }

    [Fact]
    public async Task Convert_PxaRoute_ReturnsPxaCompatibleMigrationResult()
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
        Assert.Contains("using PXA.Pdf;", body.GetProperty("pxaCode").GetString());
        Assert.True(body.GetProperty("summary").GetProperty("convertedCount").GetInt32() > 0);
        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "CANMIGDEVEXP001");
    }

    [Fact]
    public async Task Convert_PdfProviderKeyAlias_AsposePdf_ReturnsPxaCompatibleMigrationResult()
    {
        var json = JsonSerializer.Serialize(new
        {
            framework = "aspose-pdf",
            sourceCode = """
                using Aspose.Pdf;
                using Aspose.Pdf.Text;

                var document = new Document();
                var page = document.Pages.Add();
                page.Paragraphs.Add(new TextFragment("Smoke"));
                document.Save(outputStream);
                """
        });

        var response = await client.PostAsync("/api/pxa/migration/convert", Json(json));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("using PXA.Pdf;", body.GetProperty("pxaCode").GetString());
        Assert.Contains("var document = new PdfDocument();", body.GetProperty("pxaCode").GetString());
        Assert.True(body.GetProperty("summary").GetProperty("convertedCount").GetInt32() > 0);
        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "CANMIGASPOSE003");
    }

    [Fact]
    public async Task Convert_SpreadsheetProviderKeyAlias_AsposeCells_ReturnsPxaWorkbookMigrationResult()
    {
        var json = JsonSerializer.Serialize(new
        {
            framework = "aspose-cells",
            sourceCode = """
                using Aspose.Cells;

                var wb = new Workbook();
                var ws = wb.Worksheets[0];
                ws.Cells["A1"].PutValue("Item");
                ws.Cells[0, 1].PutValue(10);
                ws.Cells["B2"].Formula = "=SUM(B1:B1)";
                wb.Save("out.xlsx");
                """
        });

        var response = await client.PostAsync("/api/pxa/migration/convert", Json(json));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var pxaCode = body.GetProperty("pxaCode").GetString();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("new PxaWorkbook()", pxaCode);
        Assert.Contains("ws.Cell(\"A1\").Value(\"Item\")", pxaCode);
        Assert.Contains("ws.Cell(0, 1).Value(10)", pxaCode);
        Assert.True(body.GetProperty("summary").GetProperty("convertedCount").GetInt32() > 0);
        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "CANMIGASPC011");
    }

    [Fact]
    public async Task ReportToDesign_DevExpressRepx_ReturnsEditableDesign()
    {
        var json = JsonSerializer.Serialize(new
        {
            sourceCode = """
                <?xml version="1.0" encoding="utf-8"?>
                <XtraReportsLayoutSerializer SerializerVersion="23.1.5.0" Ref="1" ControlType="DevExpress.XtraReports.UI.XtraReport, DevExpress.XtraReports.v23.1" Name="InvoiceReport" PaperKind="Letter">
                  <Bands>
                    <Item1 Ref="2" ControlType="DevExpress.XtraReports.UI.DetailBand, DevExpress.XtraReports.v23.1" Name="Detail" HeightF="100">
                      <Controls>
                        <Item1 Ref="3" ControlType="DevExpress.XtraReports.UI.XRLabel, DevExpress.XtraReports.v23.1" Name="title" Text="Invoice" SizeF="200,40" LocationFloat="50,20" />
                      </Controls>
                    </Item1>
                  </Bands>
                </XtraReportsLayoutSerializer>
                """
        });

        var response = await client.PostAsync("/api/pxa/migration/report-to-design", Json(json));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var design = body.GetProperty("design");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("InvoiceReport", design.GetProperty("name").GetString());
        Assert.Contains(
            design.GetProperty("pages")[0].GetProperty("elements").EnumerateArray(),
            element => element.GetProperty("name").GetString() == "title");
        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "CANMIGDEVREP001");
    }

    [Fact]
    public async Task ReportToDesign_Rdl_ReturnsEditableDesign()
    {
        var json = JsonSerializer.Serialize(new
        {
            sourceCode = """
                <?xml version="1.0" encoding="utf-8"?>
                <Report xmlns="http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition" Name="Invoice">
                  <Body>
                    <ReportItems>
                      <Textbox Name="customer">
                        <Top>0in</Top><Left>1in</Left><Height>0.3in</Height><Width>3in</Width>
                        <Paragraphs><Paragraph><TextRuns><TextRun><Value>=Fields!CustomerName.Value</Value></TextRun></TextRuns></Paragraph></Paragraphs>
                      </Textbox>
                    </ReportItems>
                    <Height>2in</Height>
                  </Body>
                  <Page><PageHeight>11in</PageHeight><PageWidth>8.5in</PageWidth></Page>
                </Report>
                """
        });

        var response = await client.PostAsync("/api/pxa/migration/report-to-design", Json(json));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var design = body.GetProperty("design");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Invoice", design.GetProperty("name").GetString());
        Assert.Contains(
            design.GetProperty("pages")[0].GetProperty("elements").EnumerateArray(),
            element => element.GetProperty("name").GetString() == "customer");
        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "CANMIGRDL001");
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
