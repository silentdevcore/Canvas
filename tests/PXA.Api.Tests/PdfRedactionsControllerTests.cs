using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Importer.Redaction;
using PXA.Importer.Signing;
using PXA.Pdf;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

public sealed class PdfRedactionsControllerTests
{
    [Fact]
    public async Task Preflight_ReturnsCoverageEvidenceForARegion()
    {
        var response = await CreateController().Preflight(
            CreateFile(out var bytes),
            """[{"pageNumber":1,"x":0,"y":0,"width":1000,"height":1000}]""",
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var report = Assert.IsType<PdfRedactionPreflightReport>(ok.Value);
        Assert.Equal(bytes.Length, report.FileSizeBytes);
        Assert.Equal(64, report.Sha256.Length);
        var region = Assert.Single(report.Regions);
        Assert.Contains(region.Covered, static item => item.Kind == "text");
    }

    [Fact]
    public async Task Preflight_RejectsMissingFileWithStableDiagnostic()
    {
        var response = await CreateController().Preflight(null, "[]", CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status400BadRequest, "PXAPDFREDACTAPI001");
    }

    [Fact]
    public async Task Preflight_RejectsMissingRegions()
    {
        var response = await CreateController().Preflight(CreateFile(out _), null, CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status400BadRequest, "PXAPDFREDACTAPI004");
    }

    [Fact]
    public async Task Preflight_RejectsMalformedRegionJson()
    {
        var response = await CreateController().Preflight(CreateFile(out _), "{not json", CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status400BadRequest, "PXAPDFREDACTAPI004");
    }

    [Fact]
    public async Task Preflight_RejectsNonFiniteRegionValues()
    {
        var response = await CreateController().Preflight(
            CreateFile(out _),
            """[{"pageNumber":1,"x":0,"y":0,"width":1e400,"height":10}]""",
            CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status400BadRequest, "PXAPDFREDACTAPI004");
    }

    [Fact]
    public async Task Preflight_RejectsAnInvalidPageNumber()
    {
        var response = await CreateController().Preflight(
            CreateFile(out _),
            """[{"pageNumber":0,"x":0,"y":0,"width":10,"height":10}]""",
            CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status400BadRequest, "PXAPDFREDACTAPI004");
    }

    [Fact]
    public async Task Apply_ReturnsRedactedPdfWithAuditHeaders()
    {
        var controller = CreateController();

        var response = await controller.Apply(
            CreateFile(out _),
            """[{"pageNumber":1,"x":0,"y":0,"width":1000,"height":1000}]""",
            "Personal data",
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(response);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.NotEmpty(file.FileContents);
        Assert.Equal(64, controller.Response.Headers["PXA-Redaction-Audit"].ToString().Length);
        Assert.Equal(64, controller.Response.Headers["PXA-Redaction-Result-Sha256"].ToString().Length);
    }

    [Fact]
    public async Task Apply_ReturnsConflictWhenPreflightBlocks()
    {
        // A region that clips the text run only partially cannot be applied.
        var response = await CreateController().Apply(
            CreateFile(out _),
            """[{"pageNumber":1,"x":0,"y":0,"width":60,"height":1000}]""",
            null,
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
        Assert.Equal("PXAPDFREDACTAPI006", Assert.Contains("code", details.Extensions)?.ToString());
        Assert.IsType<PdfRedactionPreflightReport>(Assert.Contains("preflight", details.Extensions));
    }

    [Fact]
    public async Task Apply_RejectsMissingFile()
    {
        var response = await CreateController().Apply(null, "[]", null, CancellationToken.None);

        AssertProblem(response, StatusCodes.Status400BadRequest, "PXAPDFREDACTAPI001");
    }

    [Fact]
    public void Apply_RequiresTheApplyPermissionSeparatelyFromInspect()
    {
        var apply = typeof(PdfRedactionsController).GetMethod(nameof(PdfRedactionsController.Apply))!;
        var authorization = Assert.Single(
            apply.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());

        Assert.Equal(PxaDesignerPermissions.PdfRedactionsApply, authorization.Policy);
        Assert.Equal(
            PxaDesignerPermissions.PdfRedactionsApply,
            PxaDesignerPermissionResolver.Resolve("/api/pxa/v1/pdf/redactions/apply", HttpMethods.Post));
        // Applying redaction destroys content irreversibly, so it sits above document editing.
        Assert.Contains(PxaDesignerPermissions.PdfRedactionsApply, PxaRoles.Permissions[PxaRoles.Manager]);
        Assert.DoesNotContain(PxaDesignerPermissions.PdfRedactionsApply, PxaRoles.Permissions[PxaRoles.Editor]);
    }

    [Fact]
    public void Controller_RequiresRedactionPermissionAndRateLimit()
    {
        var type = typeof(PdfRedactionsController);
        var authorization = Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        var rateLimit = Assert.Single(type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Cast<EnableRateLimitingAttribute>());

        Assert.Equal(PxaDesignerPermissions.PdfRedactionsInspect, authorization.Policy);
        Assert.Equal("designer-code", rateLimit.PolicyName);
        Assert.Contains(PxaDesignerPermissions.PdfRedactionsInspect, PxaRoles.Permissions[PxaRoles.Editor]);
        // Preflight begins a destructive workflow, so a read-only role must not hold it.
        Assert.DoesNotContain(PxaDesignerPermissions.PdfRedactionsInspect, PxaRoles.Permissions[PxaRoles.Viewer]);
    }

    [Fact]
    public void Preflight_ResolvesToTheRedactionPermission() =>
        Assert.Equal(
            PxaDesignerPermissions.PdfRedactionsInspect,
            PxaDesignerPermissionResolver.Resolve("/api/pxa/v1/pdf/redactions/preflight", HttpMethods.Post));

    private static PdfRedactionsController CreateController()
    {
        var preflight = new PdfRedactionPreflight();
        return new PdfRedactionsController(preflight, new PdfRedactionApplier(preflight), new TestAuditKeyProvider())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static IFormFile CreateFile(out byte[] bytes)
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("API redaction preflight", 40, 40);
        bytes = document.ToBytes();
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "redaction.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    private static void AssertProblem(IActionResult? result, int statusCode, string code)
    {
        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(statusCode, problem.StatusCode);
        var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
        Assert.Equal(code, Assert.Contains("code", details.Extensions)?.ToString());
    }

    private sealed class TestAuditKeyProvider(IPdfSigningKey? key = null) : IPxaRedactionAuditKeyProvider
    {
        public IPdfSigningKey? Key { get; } = key;
    }
}
