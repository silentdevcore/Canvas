using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PVAD.Migration.Pdf.Viewer.Nutrient;
using PVAD.WebApi.Controllers;

namespace PVAD.Api.Tests;

public sealed class NutrientViewerMigrationControllerTests
{
    [Fact]
    public void Convert_ReturnsPvadSidecarForInstantJson()
    {
        var request = new NutrientViewerMigrationRequest(
            "instantJson",
            """
                {
                  "format": "https://pspdfkit.com/instant-json/v1",
                  "annotations": [{
                    "id": "note-1",
                    "type": "pspdfkit/comment/note",
                    "pageIndex": 0,
                    "bbox": [10, 20, 30, 40],
                    "text": { "value": "Review" }
                  }]
                }
                """,
            [new NutrientPageGeometry(100, 200)],
            "synthetic.pdf");

        var response = new NutrientViewerMigrationController().Convert(request);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<NutrientInstantJsonResult>(ok.Value);
        Assert.Equal("nutrient-viewer", result.ProviderKey);
        Assert.Contains("\"xPct\": 10", result.AnnotationSidecarJson);
        Assert.Contains("\"text\": \"Review\"", result.AnnotationSidecarJson);
    }

    [Fact]
    public void Convert_ReturnsNormalizedViewerConfigurationForJavaScript()
    {
        var request = new NutrientViewerMigrationRequest(
            "javascript",
            "NutrientViewer.load({ document: '/files/sample.pdf', initialViewState: { currentPageIndex: 1 } });");

        var response = new NutrientViewerMigrationController().Convert(request);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<NutrientViewerSourceResult>(ok.Value);
        Assert.Contains("\"initialPageNumber\": 2", result.NormalizedViewerSettingsJson);
        Assert.Contains("openMigratedPdfViewer", result.GeneratedIntegrationModule);
    }

    [Fact]
    public void Convert_AcceptsTypeScriptCompilerOutput()
    {
        var request = new NutrientViewerMigrationRequest(
            "typescript",
            "const config: object = { document: '/typed.pdf' }; NutrientViewer.load(config);",
            TranspiledSource: "const config = { document: '/typed.pdf' }; NutrientViewer.load(config);");

        var response = new NutrientViewerMigrationController().Convert(request);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<NutrientViewerSourceResult>(ok.Value);
        Assert.Equal("typescript", result.SourceLanguage);
        Assert.Contains("/typed.pdf", result.NormalizedViewerSettingsJson);
    }

    [Fact]
    public void GetComprehensiveExample_ReturnsAllBetaInputFamilies()
    {
        var response = new NutrientViewerMigrationController().GetComprehensiveExample();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var example = Assert.IsType<NutrientComprehensiveExample>(ok.Value);
        Assert.Contains("NutrientViewer.load", example.TypeScript);
        Assert.Contains("instant-json/v1", example.InstantJson);
        Assert.Contains("GdPicturePDF", example.GdPictureCSharp);
        Assert.Equal(2, example.Pages.Count);
    }

    [Fact]
    public void NutrientCodeExample_ReturnsComprehensiveGdPictureSource()
    {
        var response = new NutrientCodeMigrationController().GetComprehensiveExample();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var example = Assert.IsType<NutrientCodeComprehensiveExample>(ok.Value);
        Assert.Contains("GdPicturePDF", example.GdPictureCSharp);
        Assert.Contains("ApplyRedaction", example.GdPictureCSharp);
        Assert.Contains("SignPDF", example.GdPictureCSharp);
    }
}
