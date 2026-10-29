using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PVAD.Importer.Redaction;
using PVAD.Importer.Signing;
using PVAD.Pdf;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services;
using PVAD.WebApi.Services.Pdf;

namespace PVAD.Api.Tests;

/// <summary>
/// Exercises the Viewer redaction endpoints against the controller directly. These
/// deliberately avoid <c>WebApplicationFactory</c>: every host it builds opens its own
/// database connection pool, and the suite already runs close to the server's connection
/// limit.
/// </summary>
public sealed class PdfViewerRedactionControllerTests : IDisposable
{
    [Theory]
    [InlineData(1, 409, "PXAPDFREDACTVIEWER001")]
    [InlineData(2, 409, "PXAPDFREDACTVIEWER002")]
    [InlineData(3, 503, "PXAPDFREDACTVIEWER003")]
    public async Task RedactionFailures_AddAliasesWithoutReturningPdf(int mode, int status, string code)
    {
        var response = await CreateController(new FailureApplier(mode)).Redact(
            CreateFile(), Sidecar(widthPct: 100), null, CancellationToken.None);
        var failure = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(status, failure.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(failure.Value));
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.Equal("PVAD" + code[3..], json.RootElement.GetProperty("canonicalCode").GetString());
        Assert.True(json.RootElement.TryGetProperty("error", out _));
        Assert.False(json.RootElement.TryGetProperty("bytes", out _));
    }

    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "pxa-viewer-redaction-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Redact_SplitsAPartiallyCoveredTextRun()
    {
        // 20% of a 300pt page is 60pt, which cuts through the run starting at x=24 rather
        // than containing it.
        var controller = CreateController();
        var response = await controller.Redact(
            CreateFile(),
            Sidecar(widthPct: 20),
            "Privacy request",
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(response);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(64, controller.Response.Headers["PXA-Redaction-Audit"].ToString().Length);
        Assert.DoesNotContain("Source PDF", System.Text.Encoding.Latin1.GetString(file.FileContents), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redact_RemovesCoveredTextAndReturnsAuditHeaders()
    {
        var controller = CreateController();

        var response = await controller.Redact(
            CreateFile(),
            Sidecar(widthPct: 100),
            "Privacy request",
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(response);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(64, controller.Response.Headers["PXA-Redaction-Audit"].ToString().Length);
        Assert.Equal(64, controller.Response.Headers["PXA-Redaction-Result-Sha256"].ToString().Length);

        var content = System.Text.Encoding.Latin1.GetString(file.FileContents);
        Assert.DoesNotContain("Source PDF", content, StringComparison.Ordinal);
        // The audit travels in headers, never inside the document it describes.
        Assert.DoesNotContain("Privacy request", content, StringComparison.Ordinal);
        Assert.DoesNotContain("RedactionAudit", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RedactPreflight_ReportsCoverageWithoutChangingTheDocument()
    {
        var response = await CreateController().RedactPreflight(
            CreateFile(),
            Sidecar(widthPct: 100),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var report = Assert.IsType<PdfRedactionPreflightReport>(ok.Value);
        Assert.Equal(64, report.Sha256.Length);
        Assert.NotEmpty(report.Regions);
        Assert.Contains(Assert.Single(report.Regions).Covered, static item => item.Kind == "text");
    }

    [Fact]
    public async Task Redact_RejectsASidecarWithoutRedactionMarks()
    {
        var response = await CreateController().Redact(
            CreateFile(),
            """{"version":1,"annotations":[{"id":"n1","type":"note","pageNumber":1}]}""",
            null,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
    }

    [Fact]
    public void Redact_ResolvesToTheViewerRedactPermission()
    {
        Assert.Equal(
            PvadDesignerPermissions.PdfViewerRedact,
            PvadDesignerPermissionResolver.Resolve("/api/pdf-viewer/annotations/redact", HttpMethods.Post));
        Assert.Equal(
            PvadDesignerPermissions.PdfViewerRedact,
            PvadDesignerPermissionResolver.Resolve("/api/pdf-viewer/annotations/redact/preflight", HttpMethods.Post));
    }

    [Theory]
    [InlineData(0, 40, 140, 60, 40)]
    [InlineData(90, 70, 40, 60, 40)]
    [InlineData(180, 220, 60, 60, 40)]
    [InlineData(270, 190, 160, 60, 40)]
    public void RedactionRegionMapper_ShouldMapCropBoxAndRotation(
        int rotation,
        double expectedX,
        double expectedY,
        double expectedWidth,
        double expectedHeight)
    {
        var mapped = PdfViewerRedactionRegionMapper.MapViewerRectangle(
            new PVAD.Importer.Graphics.PdfRectangle(10, 20, 300, 200),
            rotation,
            xPct: 10,
            yPct: 20,
            widthPct: 20,
            heightPct: 20);

        Assert.Equal(expectedX, mapped.X, 6);
        Assert.Equal(expectedY, mapped.Y, 6);
        Assert.Equal(expectedWidth, mapped.Width, 6);
        Assert.Equal(expectedHeight, mapped.Height, 6);
    }

    private PdfViewerAnnotationsController CreateController(IPdfRedactionApplier? applier = null)
    {
        var preflight = new PdfRedactionPreflight();
        return new PdfViewerAnnotationsController(
            new PdfViewerAnnotationStore(_storageRoot),
            new PdfViewerAnnotationFlatteningService(),
            new PdfViewerNativeAnnotationExtractionService(),
            preflight,
            applier ?? new PdfRedactionApplier(preflight),
            new PdfViewerRedactionRegionMapper(),
            new TestAuditKeyProvider(),
            new EmptyTimestampAuthorityFactory())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private sealed class FailureApplier(int mode) : IPdfRedactionApplier
    {
        public async Task<PdfRedactionApplyResult> ApplyAsync(Stream pdfStream,
            IReadOnlyList<PdfRedactionRegion> regions, PdfRedactionApplyOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (mode == 3) throw new System.Security.Cryptography.CryptographicException("Synthetic failure");
            var result = await new PdfRedactionApplier(new PdfRedactionPreflight())
                .ApplyAsync(pdfStream, regions, options, cancellationToken);
            if (mode == 1) throw new PdfRedactionBlockedException("Synthetic refusal", result.Preflight);
            return result with { Verification = result.Verification with { Passed = false } };
        }
    }

    private static string Sidecar(double widthPct) => $$"""
        {
          "version": 1,
          "annotations": [
            {
              "id": "redaction1",
              "type": "redaction",
              "pageNumber": 1,
              "xPct": 0,
              "yPct": 0,
              "widthPct": {{widthPct}},
              "heightPct": 100,
              "reason": "Privacy request",
              "author": "Reviewer",
              "color": "#111827"
            }
          ]
        }
        """;

    private static IFormFile CreateFile()
    {
        var document = new PdfDocument();
        document.AddPage(300, 180).DrawTextFromTop("Source PDF", 24, 24, 12);
        using var buffer = new MemoryStream();
        document.Save(buffer);
        var bytes = buffer.ToArray();
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "review.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private sealed class TestAuditKeyProvider(IPdfSigningKey? key = null) : IPvadRedactionAuditKeyProvider
    {
        public IPdfSigningKey? Key { get; } = key;
    }

    private sealed class EmptyTimestampAuthorityFactory : IPvadPdfTimestampAuthorityFactory
    {
        public IPdfTimestampAuthority? Create() => null;
    }
}
