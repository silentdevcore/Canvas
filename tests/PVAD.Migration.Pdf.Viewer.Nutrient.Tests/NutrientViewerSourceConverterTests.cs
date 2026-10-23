using System.Text.Json.Nodes;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Pdf.Viewer.Nutrient;

namespace PVAD.Migration.Pdf.Viewer.Nutrient.Tests;

public sealed class NutrientViewerSourceConverterTests
{
    [Fact]
    public void Convert_MapsCurrentViewerConfigurationWithoutExecutingSource()
    {
        const string source = """
            import NutrientViewer from "@nutrient-sdk/viewer";

            const config = {
              container: "#viewer",
              document: "/documents/invoice.pdf",
              locale: "de",
              theme: "DARK",
              initialViewState: { currentPageIndex: 2, zoom: "FIT_TO_WIDTH" },
              toolbarItems: ["pager", "zoom-out", "print"]
            };

            NutrientViewer.load(config);
            """;

        var result = new NutrientViewerSourceConverter().Convert(source);
        var settings = JsonNode.Parse(result.NormalizedViewerSettingsJson)!.AsObject();

        Assert.Equal("Nutrient Web SDK", result.DetectedProduct);
        Assert.Equal("#viewer", settings["mountSelector"]!.GetValue<string>());
        Assert.Equal("/documents/invoice.pdf", settings["documentSource"]!.GetValue<string>());
        Assert.Equal(3, settings["initialPageNumber"]!.GetValue<int>());
        Assert.Equal("width", settings["initialFitMode"]!.GetValue<string>());
        Assert.Contains("openMigratedPdfViewer", result.GeneratedIntegrationModule);
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB033");
        Assert.Equal("reviewRequired", result.DocumentFidelity);
    }

    [Fact]
    public void Convert_DetectsHistoricalPspdfkitGlobal()
    {
        var result = new NutrientViewerSourceConverter().Convert(
            "PSPDFKit.load({ container: '#app', document: './sample.pdf' });");

        Assert.Equal("PSPDFKit Web SDK", result.DetectedProduct);
        Assert.Contains("./sample.pdf", result.NormalizedViewerSettingsJson);
        Assert.Single(result.SourceMap);
        Assert.Equal(1, result.SourceMap[0].SourceLine);
    }

    [Fact]
    public void Convert_DetectsImportAlias()
    {
        var result = new NutrientViewerSourceConverter().Convert(
            "import Viewer from '@nutrient-sdk/viewer';\nViewer.load({ document: '/a.pdf' });");

        Assert.Equal("Nutrient Web SDK", result.DetectedProduct);
        Assert.Equal(2, result.SourceMap[0].SourceLine);
    }

    [Fact]
    public void Convert_MapsConstructedViewStateAndReportsEventIntegration()
    {
        const string source = """
            const config = {
              document: "/a.pdf",
              initialViewState: new NutrientViewer.ViewState({ currentPageIndex: 4, zoom: "FIT_TO_WIDTH" }),
              scrollMode: "PER_SPREAD"
            };
            NutrientViewer.load(config).then(instance => {
              instance.addEventListener("viewState.change", handleViewState);
            });
            """;

        var result = new NutrientViewerSourceConverter().Convert(source);

        Assert.Contains("\"initialPageNumber\": 5", result.NormalizedViewerSettingsJson);
        Assert.Contains("\"initialFitMode\": \"width\"", result.NormalizedViewerSettingsJson);
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB034");
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB035");
    }

    [Fact]
    public void Convert_RemovesLicenseAndRemoteServerSecrets()
    {
        const string source = """
            NutrientViewer.load({
              document: "https://customer.example/private.pdf",
              licenseKey: "secret-license",
              serverUrl: "https://document-engine.example",
              jwt: "secret-jwt"
            });
            """;

        var result = new NutrientViewerSourceConverter().Convert(source);

        Assert.DoesNotContain("secret-license", result.NormalizedViewerSettingsJson);
        Assert.DoesNotContain("secret-jwt", result.NormalizedViewerSettingsJson);
        Assert.DoesNotContain("customer.example", result.NormalizedViewerSettingsJson);
        Assert.DoesNotContain("secret-license", result.GeneratedIntegrationModule);
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB030");
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB031");
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB032");
    }

    [Fact]
    public void Convert_ReportsDynamicValuesInsteadOfExecutingThem()
    {
        var result = new NutrientViewerSourceConverter().Convert(
            "NutrientViewer.load({ document: getDocumentUrl(), locale: window.locale });");

        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB021");
        Assert.DoesNotContain("getDocumentUrl", result.GeneratedIntegrationModule);
    }

    [Theory]
    [InlineData("//evil.example/document.pdf")]
    [InlineData("../private/document.pdf")]
    [InlineData("C:\\private\\document.pdf")]
    public void Convert_DoesNotCopyExternalOrUnsafeDocumentPaths(string document)
    {
        var source = $"NutrientViewer.load({{ document: {JsonValue.Create(document)!.ToJsonString()} }});";

        var result = new NutrientViewerSourceConverter().Convert(source);

        Assert.DoesNotContain(document, result.NormalizedViewerSettingsJson);
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB030");
    }

    [Fact]
    public void ConvertTranspiledTypeScript_UsesCompilerOutputWithoutExecutingSource()
    {
        const string source = "const config: Configuration = { document: '/typed.pdf' }; NutrientViewer.load(config);";
        const string transpiled = "const config = { document: '/typed.pdf' }; NutrientViewer.load(config);";

        var result = new NutrientViewerSourceConverter().ConvertTranspiledTypeScript(source, transpiled);

        Assert.Equal("typescript", result.SourceLanguage);
        Assert.Contains("/typed.pdf", result.NormalizedViewerSettingsJson);
        Assert.NotEqual(result.SourceChecksum, result.NormalizedChecksum);
    }

    [Fact]
    public void ComprehensiveTypeScriptFixture_MapsViewerAndReportsApplicationCallbacks()
    {
        const string transpiled = """
            import NutrientViewer from "@nutrient-sdk/viewer";
            const config = {
              container: "#nutrient-viewer",
              document: "/documents/booking-receipt.pdf",
              locale: "en",
              initialViewState: { currentPageIndex: 1, zoom: "FIT_TO_WIDTH" },
              toolbarItems: ["pager", "search", "print"],
              scrollMode: "PER_SPREAD"
            };
            NutrientViewer.load(config).then(instance => instance.addEventListener("document.loaded", onReady));
            """;

        var result = new NutrientViewerSourceConverter().ConvertTranspiledTypeScript(
            NutrientViewerExamples.ComprehensiveTypeScript,
            transpiled,
            "comprehensive.ts");

        Assert.Equal("Nutrient Web SDK", result.DetectedProduct);
        Assert.Equal("typescript", result.SourceLanguage);
        Assert.Contains("booking-receipt.pdf", result.NormalizedViewerSettingsJson);
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB034");
        Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB035");
        Assert.Equal("reviewRequired", result.DocumentFidelity);
    }

    [Fact]
    public void Convert_ReportsTrustFeaturesWithoutClaimingAutomaticParity()
    {
        const string source = """
            NutrientViewer.load({ document: "/a.pdf", toolbarItems: ["signature", "redaction"] });
            """;

        var result = new NutrientViewerSourceConverter().Convert(source);

        Assert.NotNull(result.PdfTrust);
        Assert.Equal(PdfTrustMigrationDisposition.Unsupported, result.PdfTrust.OverallDisposition);
        Assert.Contains(result.PdfTrust.Evidence, item =>
            item.Capability == PdfTrustCapability.DigitalSignature &&
            item.Disposition == PdfTrustMigrationDisposition.ReviewRequired);
        Assert.Contains(result.PdfTrust.Evidence, item =>
            item.Capability == PdfTrustCapability.SecureRedaction &&
            item.SourceSemantics == PdfTrustSourceSemantics.VisualRedactionOnly &&
            item.Disposition == PdfTrustMigrationDisposition.Unsupported);
    }

    [Fact]
    public void Convert_RejectsSourceWithoutViewerBootstrap()
    {
        var exception = Assert.Throws<NutrientViewerSourceException>(() =>
            new NutrientViewerSourceConverter().Convert("console.log('hello');"));

        Assert.Equal("CANMIGNUTWEB005", exception.Code);
    }
}
