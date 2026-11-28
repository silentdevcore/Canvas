using System.Net;
using System.Text;
using System.Text.Json;
using PVAD.Licensing;
using PVAD.Core.Contracts;
using PVAD.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PVAD.FileImporter.ImageOcr;
using SkiaSharp;

namespace PVAD.Api.Tests;

[CollectionDefinition("Native importer license renewal", DisableParallelization = true)]
public sealed class NativeImporterLicenseCollection { }

[Collection("Native importer license renewal")]
public sealed class PvadNativeImporterLicenseTests
{
    [Fact]
    public async Task Designer_pdf_import_preserves_mixed_layout_and_requires_importer_rights_per_request()
    {
        var integrated = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Run with a signed native test session.");
        using var factory = new PvadApiTestFactory();
        using var client = factory.CreateCompanyProductClient();
#pragma warning disable PVAD0001 // Synthetic PDF preparation uses the integrated test grant.
        var document = new PVAD.Pdf.PdfDocument();
#pragma warning restore PVAD0001
        document.AddPage(595, 842).DrawText("Synthetic portrait body", 40, 350);
        document.AddPage(842, 595).DrawText("Synthetic landscape body", 40, 300);
        var bytes = document.ToBytes();
        try
        {
            foreach (var product in new[] { "importer", "pdf", "importer" })
            {
                PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, product + ".json"));
                using var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(bytes), "file", "synthetic.pdf");
                using var response = await client.PostAsync("/api/pvad/document/import-pdf-engine", form);
                var content = await response.Content.ReadAsStringAsync();
                if (product == "pdf")
                {
                    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                    using var problem = JsonDocument.Parse(content);
                    Assert.Equal("PVADLIC012", problem.RootElement.GetProperty("code").GetString());
                    Assert.Null(response.Content.Headers.ContentDisposition);
                    continue;
                }
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var design = JsonSerializer.Deserialize<DesignExportDto>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                Assert.Equal(2, design.Pages.Count);
                Assert.Equal(595d, design.Pages[0].Layout!.Width);
                Assert.Equal(842d, design.Pages[1].Layout!.Width);
                Assert.Contains(design.Pages[0].Elements, e => e.Content == "Synthetic portrait body");
                Assert.Contains(design.Pages[1].Elements, e => e.Content == "Synthetic landscape body");
            }
        }
        finally { PvadLicense.LoadFromFile(integrated); }
    }

    [Fact]
    public async Task Image_analysis_and_OCR_PDF_output_accept_importer_only_rights()
    {
        var integrated = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Run with a signed native test session.");
        var engine = new SyntheticOcrEngine();
        using var root = new PvadApiTestFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOcrEngine>();
            services.AddSingleton<IOcrEngine>(engine);
        }));
        using var client = factory.CreateCompanyProductClient();
        using var bitmap = new SKBitmap(160, 80);
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = encoded.ToArray();
        try
        {
            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "importer.json"));
            using var analysisUpload = ImageUpload();
            analysisUpload.Add(new StringContent("true"), "includeDebugOverlay");
            using var analysis = await client.PostAsync("/api/pvad/document/import-image-analysis", analysisUpload);
            Assert.Equal(HttpStatusCode.OK, analysis.StatusCode);
            using var analysisJson = JsonDocument.Parse(await analysis.Content.ReadAsStringAsync());
            Assert.StartsWith("data:image/png;base64,", analysisJson.RootElement.GetProperty("debugOverlay").GetString());

            using var debugUpload = ImageUpload();
            using var debug = await client.PostAsync("/api/pvad/document/convert-image-to-pdf?debug=true", debugUpload);
            Assert.Equal(HttpStatusCode.OK, debug.StatusCode);
            using var debugJson = JsonDocument.Parse(await debug.Content.ReadAsStringAsync());
            Assert.Equal("Synthetic", debugJson.RootElement.GetProperty("diagnostics").GetProperty("ocrEngine").GetString());

            using var pdfUpload = ImageUpload();
            using var pdf = await client.PostAsync("/api/pvad/document/convert-image-to-pdf", pdfUpload);
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
            Assert.True((await pdf.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8));
            Assert.Equal(2, engine.Calls);

            // Internal completion retains its original admission; new public calls do not.
            engine.OnRecognize = () =>
            {
                PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "pdf.json"));
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() =>
                    PvadLicense.Require(PvadLicenseProducts.Importer)).Code);
            };
            using var revokedUpload = ImageUpload();
            using var revoked = await client.PostAsync("/api/pvad/document/convert-image-to-pdf", revokedUpload);
            Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
            Assert.Equal("application/pdf", revoked.Content.Headers.ContentType?.MediaType);
            Assert.True((await revoked.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8));
            var admittedCalls = engine.Calls;
            using var newUpload = ImageUpload();
            using var denied = await client.PostAsync("/api/pvad/document/convert-image-to-pdf", newUpload);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, denied.StatusCode);
            using var problem = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
            Assert.Equal("PVADLIC012", problem.RootElement.GetProperty("code").GetString());
            Assert.Null(denied.Content.Headers.ContentDisposition);
            Assert.Equal(admittedCalls, engine.Calls);
        }
        finally { PvadLicense.LoadFromFile(integrated); }

        MultipartFormDataContent ImageUpload()
        {
            var upload = new MultipartFormDataContent();
            upload.Add(new ByteArrayContent(bytes), "file", "synthetic.png");
            return upload;
        }
    }

    [Fact]
    public async Task Debug_overlay_route_accepts_importer_only_rights()
    {
        var integrated = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Run with a signed native test session.");
        using var factory = new PvadApiTestFactory();
        using var client = factory.CreateCompanyProductClient();
#pragma warning disable PVAD0001 // Synthetic input preparation uses the integrated test grant.
        var document = new PVAD.Pdf.PdfDocument();
#pragma warning restore PVAD0001
        document.AddPage().DrawText("Synthetic debug access", 40, 40);
        var bytes = document.ToBytes();
        try
        {
            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "importer.json"));
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(bytes), "file", "synthetic.pdf");
            using var response = await client.PostAsync("/api/pvad/document/debug-pdf-engine", form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(1, json.RootElement.GetProperty("pageCount").GetInt32());
            Assert.True(json.RootElement.GetProperty("sceneGraph").GetProperty("debugOverlayCount").GetInt32() > 0);
        }
        finally { PvadLicense.LoadFromFile(integrated); }
    }

    [Fact]
    public async Task File_import_routes_propagate_missing_product_rights_and_accept_importer_only_renewal()
    {
        var integrated = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Run with a signed native test session.");
        using var factory = new PvadApiTestFactory();
        using var client = factory.CreateCompanyProductClient();
        var routes = new[]
        {
            ("import-pdf-engine", "pdf"), ("debug-pdf-engine", "pdf"), ("import-doc", "doc"),
            ("import-docx", "docx"), ("import-markdown", "md"), ("import-odt", "odt"),
            ("import-image", "png"), ("import-svg", "svg"), ("import-pptx", "pptx"),
            ("import-image-analysis", "png"), ("convert-image-to-pdf", "png"),
        };
        try
        {
            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "pdf.json"));
            foreach (var (route, extension) in routes)
            {
                using var form = Upload(extension);
                using var response = await client.PostAsync("/api/pvad/document/" + route, form);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                Assert.Null(response.Content.Headers.ContentDisposition);
                using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal("PVADLIC012", problem.RootElement.GetProperty("code").GetString());
            }

            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "importer.json"));
            using var upload = Upload("md");
            using var recovered = await client.PostAsync("/api/pvad/document/import-markdown", upload);
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
            Assert.Contains("Imported content", await recovered.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally { PvadLicense.LoadFromFile(integrated); }
    }

    private static MultipartFormDataContent Upload(string extension)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("# Imported content")), "file", "synthetic." + extension);
        return form;
    }

    private sealed class SyntheticOcrEngine : IOcrEngine
    {
        public string Name => "Synthetic";
        public string Version => "1";
        public int Calls { get; private set; }
        public Action? OnRecognize { get; set; }
        public Task<IReadOnlyList<OcrPage>> RecognizeAsync(IReadOnlyList<OcrImagePage> pages,
            ImageToPdfConversionOptions options, CancellationToken cancellationToken = default)
        {
            Calls++;
            OnRecognize?.Invoke();
            return Task.FromResult<IReadOnlyList<OcrPage>>([new OcrPage
            {
                PageIndex = 0, WidthPx = pages[0].WidthPx, HeightPx = pages[0].HeightPx,
            }]);
        }
    }
}
