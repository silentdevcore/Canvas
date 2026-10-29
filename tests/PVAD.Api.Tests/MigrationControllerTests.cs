using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Pdf.Code.Nutrient;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services;

namespace PVAD.Api.Tests;

public sealed class MigrationControllerTests : IClassFixture<PvadApiTestFactory>
{
    private readonly HttpClient client;

    public MigrationControllerTests(PvadApiTestFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task CanonicalCatalog_SeparatesDomainsKindsAndProviderFamilies()
    {
        var response = await client.GetAsync("/api/pvad/v1/migrations/catalog");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var providers = body.GetProperty("providers").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(35, providers.Length); // Includes separate Nutrient Viewer and editable Designer contracts.
        Assert.Contains(providers, item =>
            item.GetProperty("domain").GetString() == "pdf"
            && item.GetProperty("kind").GetString() == "viewer"
            && item.GetProperty("providerKey").GetString() == "nutrient-viewer"
            && item.GetProperty("providerFamily").GetString() == "nutrient");
        Assert.Contains(providers, item =>
            item.GetProperty("domain").GetString() == "spreadsheet"
            && item.GetProperty("kind").GetString() == "code"
            && item.GetProperty("providerKey").GetString() == "aspose-cells");
        Assert.Contains(providers, item =>
            item.GetProperty("domain").GetString() == "report"
            && item.GetProperty("kind").GetString() == "designer"
            && item.GetProperty("providerKey").GetString() == "devexpress-report");
        Assert.Contains(providers, item =>
            item.GetProperty("domain").GetString() == "pdf"
            && item.GetProperty("kind").GetString() == "designer"
            && item.GetProperty("providerKey").GetString() == "nutrient-viewer");
    }

    [Fact]
    public async Task CanonicalCatalog_DomainFilterNeverMixesSpreadsheetAndPdf()
    {
        var response = await client.GetAsync("/api/pvad/v1/migrations/catalog?domain=spreadsheet&kind=code");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var providers = body.GetProperty("providers").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(8, providers.Length);
        Assert.All(providers, item =>
        {
            Assert.Equal("spreadsheet", item.GetProperty("domain").GetString());
            Assert.Equal("code", item.GetProperty("kind").GetString());
        });
    }

    [Fact]
    public async Task CanonicalProviderAndExamples_ExposeBackendOwnedContracts()
    {
        var providerResponse = await client.GetAsync("/api/pvad/v1/migrations/pdf/viewer/nutrient-viewer");
        var provider = await providerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var examplesResponse = await client.GetAsync("/api/pvad/v1/migrations/pdf/viewer/nutrient-viewer/examples");
        var examples = await examplesResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, providerResponse.StatusCode);
        Assert.Equal("designer.nutrient-viewer-migration", provider.GetProperty("featureId").GetString());
        Assert.Equal(HttpStatusCode.OK, examplesResponse.StatusCode);
        Assert.NotEmpty(examples.GetProperty("examples").EnumerateArray());
    }

    [Fact]
    public async Task CanonicalViewerConvert_ReturnsOneImportablePvadPackage()
    {
        var response = await client.PostAsync(
            "/api/pvad/v1/migrations/pdf/viewer/nutrient-viewer/convert",
            Json(JsonSerializer.Serialize(new
            {
                inputType = "instantJson",
                source = """{"format":"https://pspdfkit.com/instant-json/v1","annotations":[]}""",
                pages = new[] { new { width = 595, height = 842 } }
            })));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var artifact = Assert.Single(body.GetProperty("artifacts").EnumerateArray());
        Assert.Equal("pvad-viewer-package", artifact.GetProperty("id").GetString());
        Assert.Equal("pvad-viewer-package", artifact.GetProperty("canonicalId").GetString());
        Assert.Equal("application/vnd.pvad.viewer+json", artifact.GetProperty("canonicalMediaType").GetString());
        Assert.Equal(".pvadviewer.json", artifact.GetProperty("canonicalFileExtension").GetString());
        Assert.Equal("application/vnd.pvad.viewer+json", artifact.GetProperty("mediaType").GetString());
        Assert.Equal(".pvadviewer.json", artifact.GetProperty("fileExtension").GetString());
        var package = artifact.GetProperty("content");
        Assert.Equal(1, package.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("pvad.pdf-viewer-package", package.GetProperty("kind").GetString());
        Assert.Equal("pvad.pdf-viewer-package", package.GetProperty("canonicalKind").GetString());
        Assert.Equal("nutrient", package.GetProperty("provider").GetString());
        Assert.Equal(595, package.GetProperty("pageGeometry")[0].GetProperty("width").GetDouble());
        Assert.Equal(JsonValueKind.Object, package.GetProperty("annotationSidecar").ValueKind);
        Assert.Equal(JsonValueKind.Object, package.GetProperty("formValues").ValueKind);
    }

    [Fact]
    public async Task NutrientDesignerConvert_CombinesPdfAndInstantJsonIntoOneEditableDesign()
    {
#pragma warning disable PXA0001
        var sourcePdf = new PVAD.Pdf.PdfDocument();
#pragma warning restore PXA0001
        sourcePdf.AddPage().DrawText("Original PDF", 40, 780, 12);
        var pdfBytes = sourcePdf.ToBytes();
        var instantJson = """
            {
              "format": "https://pspdfkit.com/instant-json/v1",
              "annotations": [
                {
                  "id": "review-note",
                  "type": "pspdfkit/text",
                  "pageIndex": 0,
                  "bbox": [72, 72, 160, 30],
                  "text": "Editable Nutrient annotation",
                  "creatorName": "Migration test"
                }
              ]
            }
            """;
        using var content = new MultipartFormDataContent();
        var pdfContent = new ByteArrayContent(pdfBytes);
        pdfContent.Headers.ContentType = new("application/pdf");
        content.Add(pdfContent, "pdf", "nutrient-source.pdf");
        content.Add(new StringContent("instantJson"), "inputType");
        content.Add(new StringContent(instantJson), "source");

        var response = await client.PostAsync(
            "/api/pvad/v1/migrations/pdf/designer/nutrient-viewer/convert",
            content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pdf", body.GetProperty("domain").GetString());
        Assert.Equal("designer", body.GetProperty("kind").GetString());
        var artifact = Assert.Single(body.GetProperty("artifacts").EnumerateArray());
        Assert.Equal("design", artifact.GetProperty("id").GetString());
        var design = artifact.GetProperty("content");
        Assert.Contains(
            design.GetProperty("pages")[0].GetProperty("elements").EnumerateArray(),
            element => element.GetProperty("content").GetString() == "Editable Nutrient annotation");
        Assert.True(design.TryGetProperty("nutrientViewerMigration", out _));
        Assert.Equal(1, body.GetProperty("metadata").GetProperty("mappedAnnotations").GetInt32());
    }

    [Fact]
    public async Task CanonicalCodeConvert_ReturnsTypedArtifactEnvelope()
    {
        var response = await client.PostAsync(
            "/api/pvad/v1/migrations/pdf/code/devexpress-pdf/convert",
            Json(JsonSerializer.Serialize(new
            {
                inputType = "csharp",
                source = "using DevExpress.Pdf; var processor = new PdfDocumentProcessor(); processor.CreateEmptyDocument();"
            })));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pdf", body.GetProperty("domain").GetString());
        Assert.Equal("code", body.GetProperty("kind").GetString());
        Assert.Equal("pvad-csharp", body.GetProperty("artifacts")[0].GetProperty("id").GetString());
        Assert.Contains("PVAD.Pdf", body.GetProperty("artifacts")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task CanonicalCodePreview_ReturnsTheDomainSpecificArtifact()
    {
        var response = await client.PostAsync(
            "/api/pvad/v1/migrations/pdf/code/devexpress-pdf/preview",
            Json(JsonSerializer.Serialize(new
            {
                inputType = "csharp",
                source = "using DevExpress.Pdf; var processor = new PdfDocumentProcessor(); processor.CreateEmptyDocument();"
            })));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void CanonicalCodePreview_RejectsSourceAboveProviderLimit()
    {
        var controller = CreateCanonicalController(
            "designer.migration.pdf.code.execute",
            "designer.migration.pdf.code.provider.devexpress.execute");
        var oversizedSource = new string('x', (10 * 1024 * 1024) + 1);

        var result = controller.Preview(
            "pdf", "code", "devexpress-pdf",
            new UnifiedMigrationRequest("csharp", oversizedSource));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, problem.StatusCode);
        Assert.Equal("PXAMIG010", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
        Assert.Equal("PVADMIG010", Assert.IsType<ProblemDetails>(problem.Value).Extensions["canonicalCode"]);
    }

    [Fact]
    public async Task CanonicalReportConvert_UsesExplicitProvider()
    {
        var response = await client.PostAsync(
            "/api/pvad/v1/migrations/report/designer/rdl/convert",
            Json(JsonSerializer.Serialize(new
            {
                inputType = "report",
                source = "<Report xmlns=\"http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition\" Name=\"Canonical\"><Body><Height>1in</Height></Body><Page><PageHeight>11in</PageHeight><PageWidth>8.5in</PageWidth></Page></Report>"
            })));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rdl", body.GetProperty("provider").GetString());
        Assert.Equal("design", body.GetProperty("artifacts")[0].GetProperty("id").GetString());
        Assert.Equal("Canonical", body.GetProperty("artifacts")[0].GetProperty("content").GetProperty("name").GetString());
    }

    [Fact]
    public async Task CanonicalRoute_RejectsProviderInWrongDomain()
    {
        var response = await client.PostAsync(
            "/api/pvad/v1/migrations/spreadsheet/code/devexpress-pdf/convert",
            Json("{\"source\":\"test\"}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void ProviderCatalog_HasUniqueCanonicalPathsAndAliases()
    {
        var paths = MigrationProviderCatalog.All
            .Select(item => $"{item.Domain}/{item.MigrationKind}/{item.ProviderKey}")
            .ToArray();

        Assert.Equal(paths.Length, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(MigrationProviderCatalog.All, item => string.IsNullOrWhiteSpace(item.DocumentationId));
        Assert.DoesNotContain(MigrationProviderCatalog.All, item => item.Inputs.Count == 0 || item.Artifacts.Count == 0);
        Assert.DoesNotContain(MigrationProviderCatalog.All, item => item.MaximumInputBytes <= 0);
        Assert.True(MigrationProviderCatalog.TryGet("pdf", "code", "GdPicture.NET", out var nutrient));
        Assert.Equal("nutrient", nutrient.ProviderKey);
    }

    [Fact]
    public void DesignerProviderCatalog_DeclaresProviderSpecificSourceFiles()
    {
        var expected = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["activereports-js"] = [".json"],
            ["devexpress-report"] = [".cs", ".repx"],
            ["fastreport"] = [".frx"],
            ["jasperreports"] = [".jrxml"],
            ["rdl"] = [".rdl", ".rdlc", ".rdlx"],
            ["rpx"] = [".rpx"],
            ["stimulsoft"] = [".mrt"],
            ["telerik"] = [".trdx", ".trdp"],
        };

        foreach (var (provider, extensions) in expected)
        {
            var descriptor = MigrationProviderCatalog.Get("report", "designer", provider);
            var input = Assert.Single(descriptor.Inputs);
            Assert.Equal(extensions, input.FileExtensions);
            Assert.Equal(provider is "rdl" or "telerik", input.SupportsBinary);
        }

        var devExpress = MigrationProviderCatalog.Get("report", "designer", "devexpress-report");
        Assert.DoesNotContain(".resx", devExpress.Inputs[0].FileExtensions);
    }

    [Fact]
    public void CanonicalDesignerConversion_RejectsFileFromAnotherProvider()
    {
        var controller = CreateCanonicalController(
            "designer.migration.report.designer.execute",
            "designer.migration.report.designer.provider.devexpress.execute");

        var result = controller.Convert(
            "report", "designer", "devexpress-report",
            new UnifiedMigrationRequest("report", "<jasperReport />", "report.jrxml"));

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("PXAMIG011", Assert.IsType<ProblemDetails>(problem.Value).Extensions["code"]);
        Assert.Equal("PVADMIG011", Assert.IsType<ProblemDetails>(problem.Value).Extensions["canonicalCode"]);
    }

    [Fact]
    public void CanonicalConversion_RequiresKindAndProviderPermissions()
    {
        var controller = CreateCanonicalController(
            "designer.migration.pdf.code.provider.devexpress.execute");

        var denied = controller.Convert(
            "pdf", "code", "devexpress-pdf",
            new UnifiedMigrationRequest("csharp", "using DevExpress.Pdf;"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(denied).StatusCode);
    }

    [Fact]
    public void CanonicalConversion_AcceptsMatchingKindAndProviderPermissions()
    {
        var controller = CreateCanonicalController(
            "designer.migration.pdf.code.execute",
            "designer.migration.pdf.code.provider.devexpress.execute");

        var result = controller.Convert(
            "pdf", "code", "devexpress-pdf",
            new UnifiedMigrationRequest("csharp", "using DevExpress.Pdf; var processor = new PdfDocumentProcessor();"));

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void CanonicalNutrientConversion_SerializesPdfTrustEnumsAsStrings()
    {
        var controller = CreateCanonicalController(
            "designer.migration.pdf.code.execute",
            "designer.migration.pdf.code.provider.nutrient.execute");

        var result = controller.Convert(
            "pdf", "code", "nutrient",
            new UnifiedMigrationRequest("csharp", NutrientCodeExamples.ComprehensiveGdPictureCSharp));

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var generatedCode = body.GetProperty("artifacts")[0].GetProperty("content").GetString();
        Assert.NotNull(generatedCode);
        Assert.Contains("PdfEncryptionAlgorithm.Aes256", generatedCode, StringComparison.Ordinal);
        Assert.Contains("[CANMIGNUTNET058] ApplyRedaction", generatedCode, StringComparison.Ordinal);
        Assert.Contains("[CANMIGNUTNET059] SignPDF", generatedCode, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", generatedCode, StringComparison.Ordinal);
        Assert.DoesNotContain("owner-password", generatedCode, StringComparison.Ordinal);
        Assert.DoesNotContain("certificate.pfx", generatedCode, StringComparison.Ordinal);
        var trust = body.GetProperty("pdfTrust");
        Assert.Equal("ReviewRequired", trust.GetProperty("overallDisposition").GetString());
        var action = Assert.Single(trust.GetProperty("manualActions").EnumerateArray(), item =>
            item.GetProperty("actionId").GetString() == "CANMIGNUTNET021");
        Assert.Equal("DigitalSignature", action.GetProperty("capability").GetString());
        Assert.Equal("ReviewRequired", action.GetProperty("disposition").GetString());
    }

    private static UnifiedMigrationsController CreateCanonicalController(params string[] permissions)
    {
        var controller = new UnifiedMigrationsController(
            new MigrationService(),
            new ReportMigrationService(),
            new NutrientViewerDesignMigrationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        permissions.Select(permission => new Claim(PvadClaimTypes.Permission, permission)), "test")),
                },
            },
        };
        controller.Request.Headers["X-PVAD-Application"] = "designer";
        return controller;
    }

    [Fact]
    public async Task GetFrameworks_PvadRoute_ReturnsSupportedFrameworks()
    {
        var response = await client.GetAsync("/api/pxa/migration/frameworks");
        var frameworks = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", response.Headers.GetValues("Deprecation").Single());
        Assert.Contains(response.Headers.GetValues("Link"), link =>
            link.Contains("/api/pvad/v1/migrations/catalog", StringComparison.Ordinal) &&
            link.Contains("successor-version", StringComparison.Ordinal));
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
    public async Task Convert_PvadRoute_ReturnsPvadCompatibleMigrationResult()
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
        Assert.Contains("using PVAD.Pdf;", body.GetProperty("pxaCode").GetString());
        Assert.Equal(body.GetProperty("pxaCode").GetString(), body.GetProperty("pvadCode").GetString());
        Assert.True(body.GetProperty("summary").GetProperty("convertedCount").GetInt32() > 0);
        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "CANMIGDEVEXP001");
    }

    [Fact]
    public async Task Convert_PdfProviderKeyAlias_AsposePdf_ReturnsPvadCompatibleMigrationResult()
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
        Assert.Contains("using PVAD.Pdf;", body.GetProperty("pxaCode").GetString());
        Assert.Contains("var document = new PdfDocument();", body.GetProperty("pxaCode").GetString());
        Assert.True(body.GetProperty("summary").GetProperty("convertedCount").GetInt32() > 0);
        Assert.Contains(
            body.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "CANMIGASPOSE003");
    }

    [Fact]
    public async Task Convert_SpreadsheetProviderKeyAlias_AsposeCells_ReturnsPvadWorkbookMigrationResult()
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
        Assert.Contains("new PvadWorkbook()", pxaCode);
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
