using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PXA.Importer.Redaction;
using PXA.Importer.Signing;
using PXA.Pdf;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using PXA.WebApi.Services;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

/// <summary>
/// Exercises the Viewer redaction endpoints against the controller directly. These
/// deliberately avoid <c>WebApplicationFactory</c>: every host it builds opens its own
/// database connection pool, and the suite already runs close to the server's connection
/// limit.
/// </summary>
public sealed class PdfViewerRedactionControllerTests : IDisposable
{
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
            PxaDesignerPermissions.PdfViewerRedact,
            PxaDesignerPermissionResolver.Resolve("/api/pdf-viewer/annotations/redact", HttpMethods.Post));
        Assert.Equal(
            PxaDesignerPermissions.PdfViewerRedact,
            PxaDesignerPermissionResolver.Resolve("/api/pdf-viewer/annotations/redact/preflight", HttpMethods.Post));
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
            new PXA.Importer.Graphics.PdfRectangle(10, 20, 300, 200),
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

    private PdfViewerAnnotationsController CreateController()
    {
        var preflight = new PdfRedactionPreflight();
        return new PdfViewerAnnotationsController(
            new PdfViewerAnnotationStore(_storageRoot),
            new PdfViewerAnnotationFlatteningService(),
            new PdfViewerNativeAnnotationExtractionService(),
            preflight,
            new PdfRedactionApplier(preflight),
            new PdfViewerRedactionRegionMapper(),
            new TestAuditKeyProvider(),
            new EmptyTimestampAuthorityFactory())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
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

    private sealed class TestAuditKeyProvider(IPdfSigningKey? key = null) : IPxaRedactionAuditKeyProvider
    {
        public IPdfSigningKey? Key { get; } = key;
    }

    private sealed class EmptyTimestampAuthorityFactory : IPxaPdfTimestampAuthorityFactory
    {
        public IPdfTimestampAuthority? Create() => null;
    }
}
