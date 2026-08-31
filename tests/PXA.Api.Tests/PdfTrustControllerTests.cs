using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Importer.Trust;
using PXA.Pdf;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

public sealed class PdfTrustControllerTests
{
    [Fact]
    public async Task Inspect_ReturnsBaselineReportForPdf()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("API trust report", 40, 40);
        var bytes = document.ToBytes();
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "trust.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var response = await CreateController().Inspect(file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var report = Assert.IsType<PdfTrustReport>(ok.Value);
        Assert.Equal(PdfTrustStatus.Valid, report.StructureStatus);
        Assert.Equal(bytes.Length, report.FileSizeBytes);
        Assert.Equal(report.RevisionCount, report.Revisions.Count);
        Assert.All(report.Revisions, revision => Assert.Equal(64, revision.Sha256.Length));
    }

    [Fact]
    public async Task Inspect_RejectsMissingFileWithStableDiagnostic()
    {
        var controller = CreateController();

        var response = await controller.Inspect(null, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("PXAPDFTRUSTAPI001", details.Extensions["code"]);
    }

    [Fact]
    public void Controller_RequiresTrustPermissionAndRateLimit()
    {
        var type = typeof(PdfTrustController);
        var authorization = Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        var rateLimit = Assert.Single(type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Cast<EnableRateLimitingAttribute>());

        Assert.Equal(PxaDesignerPermissions.PdfTrustInspect, authorization.Policy);
        Assert.Equal("designer-code", rateLimit.PolicyName);
        Assert.Contains(PxaDesignerPermissions.PdfTrustInspect, PxaRoles.Permissions[PxaRoles.Viewer]);
        var postRoutes = type.GetMethod(nameof(PdfTrustController.Inspect))!
            .GetCustomAttributes(typeof(HttpPostAttribute), true)
            .Cast<HttpPostAttribute>()
            .Select(attribute => attribute.Template)
            .ToArray();
        Assert.Contains("/api/pxa/v1/pdf/compliance/validate", postRoutes);

        var convertAuthorization = Assert.Single(typeof(PdfTrustController)
            .GetMethod(nameof(PdfTrustController.ConvertPdfA2b))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(PxaDesignerPermissions.PdfConvert, convertAuthorization.Policy);
    }

    [Fact]
    public async Task PreflightPdfA2b_ReturnsMachineReadableConversionReport()
    {
        var response = await CreateController().PreflightPdfA2b(CreateFile(), null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var report = Assert.IsType<PdfA2bConversionPreflightReport>(ok.Value);
        Assert.Equal(PdfComplianceConversionCapability.Ready, report.Capability);
        Assert.True(report.OriginalPreserved);
    }

    [Fact]
    public async Task PreflightPdfA2b_AcceptsThePdfA3bProfile()
    {
        var response = await CreateController().PreflightPdfA2b(CreateFile(), "PDF/A-3b", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("PDF/A-3b", Assert.IsType<PdfA2bConversionPreflightReport>(ok.Value).TargetProfile);
    }

    [Fact]
    public async Task PreflightPdfA2b_RejectsAnUnsupportedProfile()
    {
        var response = await CreateController().PreflightPdfA2b(CreateFile(), "PDF/A-4", CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
        Assert.Equal("PXAPDFACONVERTAPI006", Assert.Contains("code", details.Extensions)?.ToString());
    }

    [Fact]
    public async Task ConvertPdfA2b_ReturnsOnePdfAndComplianceHeaders()
    {
        var controller = CreateController();

        var response = await controller.ConvertPdfA2b(CreateFile(), false, null, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(response);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("trust-pdfa2b.pdf", file.FileDownloadName);
        Assert.Equal("PDF/A-2b", controller.Response.Headers["PXA-Compliance-Profile"]);
    }

    private static PdfTrustController CreateController() => new(
        new PdfTrustInspector(),
        new TestPdfA2bConverter(),
        new TestTrustPolicyProvider())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private static FormFile CreateFile()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF\n");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "trust.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    private sealed class TestTrustPolicyProvider : IPxaPdfTrustPolicyProvider
    {
        public PdfSignatureValidationPolicy Policy { get; } = new();
    }

    private sealed class TestPdfA2bConverter : IPdfA2bConverter
    {
        // Echoes the requested profile so the controller's profile plumbing is under test
        // rather than a constant.
        private static PdfA2bConversionPreflightReport ReportFor(PdfA2bConversionOptions? options) => new(
            options?.Profile == PdfArchiveProfile.PdfA3b ? "PDF/A-3b" : "PDF/A-2b",
            PdfComplianceConversionCapability.Ready,
            new string('a', 64),
            16,
            1,
            0,
            false,
            true,
            []);

        public Task<PdfA2bConversionPreflightReport> PreflightAsync(
            Stream pdfStream,
            PdfA2bConversionOptions? options = null,
            CancellationToken cancellationToken = default) => Task.FromResult(ReportFor(options));

        public Task<PdfA2bConversionResult> ConvertAsync(
            Stream pdfStream,
            PdfA2bConversionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var report = ReportFor(options);
            return Task.FromResult(new PdfA2bConversionResult(
                Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF\n"),
                report.SourceSha256,
                new string('b', 64),
                report,
                new PdfComplianceClaim("PDF/A", report.TargetProfile, true, PdfTrustStatus.Valid, "test")));
        }
    }
}
