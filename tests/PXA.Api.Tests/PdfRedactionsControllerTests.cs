using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.Importer.Redaction;
using PXA.Importer.Signing;
using PXA.Pdf;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

public sealed class PdfRedactionsControllerTests : IDisposable
{
    private readonly List<PxaDbContext> contexts = [];

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
        Assert.StartsWith(
            "/api/pxa/v1/pdf/redactions/audits/",
            controller.Response.Headers["PXA-Redaction-Audit-Location"].ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_PersistsCompleteTenantScopedAuditEvidenceThatCanBeRetrieved()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dbContext = CreateDbContext();
        using var auditKey = CreateAuditKey();
        var controller = CreateController(
            dbContext,
            new TestTenantContext(userId, organizationId),
            auditKey);

        var response = await controller.Apply(
            CreateFile(out _),
            """[{"pageNumber":1,"x":0,"y":0,"width":1000,"height":1000}]""",
            "Personal data",
            CancellationToken.None);

        Assert.IsType<FileContentResult>(response);
        var stored = Assert.Single(dbContext.AuditEvents);
        Assert.Equal(organizationId, stored.OrganizationId);
        Assert.Equal(userId, stored.ActorUserId);
        Assert.Equal("pdf.redaction.applied", stored.Action);
        Assert.Equal("pdf-redaction-audit", stored.TargetType);
        Assert.False(string.IsNullOrWhiteSpace(stored.DetailsJson));

        var retrieved = await controller.GetAudit(stored.TargetId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(retrieved.Result);
        var audit = Assert.IsType<PdfRedactionAuditRecord>(ok.Value);
        Assert.Equal(stored.TargetId, audit.RecordSha256);
        Assert.Equal(userId.ToString(), audit.Actor);
        Assert.Equal("Personal data", audit.Reason);
        Assert.True(audit.VerificationPassed);
        Assert.NotNull(audit.Signature);
        Assert.True(PdfRedactionAuditVerifier.Verify(audit, requireSignature: true));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetAudit_DoesNotRevealAnotherOrganizationsRecord()
    {
        var dbContext = CreateDbContext();
        var writer = CreateController(
            dbContext,
            new TestTenantContext(Guid.NewGuid(), Guid.NewGuid()));
        await writer.Apply(
            CreateFile(out _),
            """[{"pageNumber":1,"x":0,"y":0,"width":1000,"height":1000}]""",
            null,
            CancellationToken.None);
        var recordHash = Assert.Single(dbContext.AuditEvents).TargetId;
        var otherTenant = CreateController(
            dbContext,
            new TestTenantContext(Guid.NewGuid(), Guid.NewGuid()));

        var response = await otherTenant.GetAudit(recordHash, CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status404NotFound, "PXAPDFREDACTAPI010");
    }

    [Fact]
    public async Task GetAudit_RejectsAnInvalidRecordIdentifier()
    {
        var response = await CreateController().GetAudit("not-a-sha256", CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status400BadRequest, "PXAPDFREDACTAPI009");
    }

    [Fact]
    public async Task GetAudit_FailsClosedWhenStoredEvidenceIsMalformed()
    {
        var organizationId = Guid.NewGuid();
        var hash = new string('a', 64);
        var dbContext = CreateDbContext();
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            Action = "pdf.redaction.applied",
            TargetType = "pdf-redaction-audit",
            TargetId = hash,
            Outcome = "succeeded",
            DetailsJson = "{\"recordSha256\":\"" + hash + "\"}"
        });
        await dbContext.SaveChangesAsync();
        var controller = CreateController(
            dbContext,
            new TestTenantContext(Guid.NewGuid(), organizationId));

        var response = await controller.GetAudit(hash, CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status500InternalServerError, "PXAPDFREDACTAPI011");
    }

    [Fact]
    public async Task Apply_RejectsAnUnboundedReasonBeforeProcessing()
    {
        var response = await CreateController().Apply(
            CreateFile(out _),
            """[{"pageNumber":1,"x":0,"y":0,"width":1000,"height":1000}]""",
            new string('x', 2_001),
            CancellationToken.None);

        AssertProblem(response, StatusCodes.Status400BadRequest, "PXAPDFREDACTAPI004");
    }

    [Fact]
    public async Task Apply_FailsClosedWhenConfiguredTimestampAuthorityFails()
    {
        using var auditKey = CreateAuditKey();
        var response = await CreateController(
            auditKey: auditKey,
            timestampAuthorityFactory: new FailingTimestampAuthorityFactory()).Apply(
                CreateFile(out _),
                """[{"pageNumber":1,"x":0,"y":0,"width":1000,"height":1000}]""",
                null,
                CancellationToken.None);

        AssertProblem(response, StatusCodes.Status503ServiceUnavailable, "PXAPDFREDACTAPI012");
    }

    [Fact]
    public async Task Apply_ReturnsConflictWhenPreflightBlocks()
    {
        // The document has only one page, so this region cannot be verified or applied.
        var response = await CreateController().Apply(
            CreateFile(out _),
            """[{"pageNumber":2,"x":0,"y":0,"width":60,"height":1000}]""",
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
        var policies = apply.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();

        Assert.Equal(PxaSecurityPolicies.AppliedPdfRedaction, Assert.Single(policies));
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
        var rateLimit = Assert.Single(type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).Cast<EnableRateLimitingAttribute>());

        Assert.Equal("designer-code", rateLimit.PolicyName);
        var preflightAuthorization = Assert.Single(type.GetMethod(nameof(PdfRedactionsController.Preflight))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(PxaDesignerPermissions.PdfRedactionsInspect, preflightAuthorization.Policy);
        Assert.Contains(PxaDesignerPermissions.PdfRedactionsInspect, PxaRoles.Permissions[PxaRoles.Editor]);
        // Preflight begins a destructive workflow, so a read-only role must not hold it.
        Assert.DoesNotContain(PxaDesignerPermissions.PdfRedactionsInspect, PxaRoles.Permissions[PxaRoles.Viewer]);
    }

    [Fact]
    public void Preflight_ResolvesToTheRedactionPermission() =>
        Assert.Equal(
            PxaDesignerPermissions.PdfRedactionsInspect,
            PxaDesignerPermissionResolver.Resolve("/api/pxa/v1/pdf/redactions/preflight", HttpMethods.Post));

    [Fact]
    public void AuditRetrieval_ResolvesToTheRedactionInspectPermission() =>
        Assert.Equal(
            PxaDesignerPermissions.PdfRedactionsInspect,
            PxaDesignerPermissionResolver.Resolve(
                $"/api/pxa/v1/pdf/redactions/audits/{new string('a', 64)}",
                HttpMethods.Get));

    private PdfRedactionsController CreateController(
        PxaDbContext? dbContext = null,
        IPxaTenantContext? tenantContext = null,
        IPdfSigningKey? auditKey = null,
        IPxaPdfTimestampAuthorityFactory? timestampAuthorityFactory = null)
    {
        var preflight = new PdfRedactionPreflight();
        return new PdfRedactionsController(
            preflight,
            new PdfRedactionApplier(preflight),
            new TestAuditKeyProvider(auditKey),
            timestampAuthorityFactory ?? new EmptyTimestampAuthorityFactory(),
            dbContext ?? CreateDbContext(),
            tenantContext ?? new TestTenantContext(Guid.NewGuid(), Guid.NewGuid()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private PxaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase($"pdf-redaction-audit-{Guid.NewGuid():N}")
            .Options;
        var context = new PxaDbContext(options);
        contexts.Add(context);
        return context;
    }

    private static PdfPkcs12SigningKey CreateAuditKey()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PXA Redaction Audit API Test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
        return new PdfPkcs12SigningKey(
            certificate.Export(X509ContentType.Pkcs12, "test-password"),
            "test-password");
    }

    public void Dispose()
    {
        foreach (var context in contexts)
        {
            context.Dispose();
        }
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

    private sealed class EmptyTimestampAuthorityFactory : IPxaPdfTimestampAuthorityFactory
    {
        public IPdfTimestampAuthority? Create() => null;
    }

    private sealed class FailingTimestampAuthorityFactory : IPxaPdfTimestampAuthorityFactory
    {
        public IPdfTimestampAuthority? Create() => new FailingTimestampAuthority();
    }

    private sealed class FailingTimestampAuthority : IPdfTimestampAuthority
    {
        public Task<PdfTimestampToken> GetTimestampTokenAsync(
            ReadOnlyMemory<byte> signatureDigest,
            HashAlgorithmName digestAlgorithm,
            CancellationToken cancellationToken = default) =>
            throw new PdfTimestampAuthorityException(
                "Timestamp service unavailable.",
                new HttpRequestException("Synthetic test failure."));
    }

    private sealed record TestTenantContext(Guid? UserId, Guid? OrganizationId) : IPxaTenantContext;
}
