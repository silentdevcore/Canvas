using Microsoft.AspNetCore.Authorization;
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
    }

    private static PdfTrustController CreateController() => new(
        new PdfTrustInspector(),
        new TestTrustPolicyProvider());

    private sealed class TestTrustPolicyProvider : IPxaPdfTrustPolicyProvider
    {
        public PdfSignatureValidationPolicy Policy { get; } = new();
    }
}
