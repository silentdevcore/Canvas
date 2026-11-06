using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Entitlements;

namespace PVAD.Api.Tests;

public sealed class PvadDesignerMigrationAccessTests
{
    [Theory]
    [InlineData("/catalog?domain=pdf&kind=code", "pdf", "code", "GET")]
    [InlineData("/pdf/code/nutrient", "pdf", "code", "GET")]
    [InlineData("/pdf/viewer/nutrient", "pdf", "viewer", "HEAD")]
    [InlineData("/spreadsheet/code/aspose", "spreadsheet", "code", "GET")]
    [InlineData("/report/designer/devexpress", "report", "designer", "GET")]
    [InlineData("/pdf/code/nutrient/convert", "pdf", "code", "POST")]
    public async Task Kind_permissions_work_after_canonical_and_legacy_route_normalization(
        string suffix, string domain, string kind, string method)
    {
        var permission = PvadDesignerMigrationPermissions.KindPermission(
            domain, kind, method == "POST" ? "execute" : "read");
        foreach (var prefix in new[] { PvadLegacyApiMiddleware.CanonicalPrefix, PvadLegacyApiMiddleware.LegacyPrefix })
        {
            var (allowed, status, csrfCalls, auditCount) = await InvokeAsync(
                prefix + "/migrations" + suffix, method, permission);
            Assert.True(allowed, prefix + suffix);
            Assert.Equal(StatusCodes.Status200OK, status);
            Assert.Equal(method == "POST" ? 1 : 0, csrfCalls);
            Assert.Equal(0, auditCount);
        }
    }

    [Theory]
    [InlineData("/api/pvad/v1/migrations/pdf/code/nutrient", "GET", "designer.migration.spreadsheet.code.read")]
    [InlineData("/api/pvad/v1/migrations/pdf/viewer/nutrient", "GET", "designer.migration.pdf.code.read")]
    [InlineData("/api/pvad/v1/migrations/pdf/code/nutrient/convert", "POST", "designer.migration.pdf.code.read")]
    [InlineData("/api/pvad/v1/migrations/catalog?domain=pdf&kind=code", "GET", "designer.migration.pdf.viewer.read")]
    [InlineData("/api/pvad/v1/migrations/catalog", "GET", "designer.migration.pdf.code.read")]
    [InlineData("/api/pvad/v10/migrations/pdf/code/nutrient", "GET", "designer.migration.pdf.code.read")]
    public async Task Unrelated_or_insufficient_permissions_remain_denied(string path, string method, string permission)
    {
        var (allowed, status, csrfCalls, auditCount) = await InvokeAsync(path, method, permission);
        Assert.False(allowed);
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal(0, csrfCalls);
        Assert.Equal(1, auditCount);
    }

    [Fact]
    public async Task Canonical_migration_permission_does_not_skip_csrf_validation()
    {
        var (allowed, status, csrfCalls, _) = await InvokeAsync(
            "/api/pvad/v1/migrations/pdf/code/nutrient/convert", "POST",
            "designer.migration.pdf.code.execute", rejectCsrf: true);
        Assert.False(allowed);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(1, csrfCalls);
    }

    private static async Task<(bool Allowed, int Status, int CsrfCalls, int AuditCount)> InvokeAsync(
        string path, string method, string permission, bool rejectCsrf = false)
    {
        await using var db = new PvadDbContext(new DbContextOptionsBuilder<PvadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var uri = new Uri("https://designer.example.test" + path);
        var context = new DefaultHttpContext();
        context.Request.Path = uri.AbsolutePath;
        context.Request.QueryString = new QueryString(uri.Query);
        context.Request.Method = method;
        context.Request.Headers["X-PVAD-Application"] = "designer";
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(PvadClaimTypes.ActiveOrganization, Guid.NewGuid().ToString()),
            new Claim(PvadClaimTypes.Permission, permission),
        ], "test"));
        var allowed = false;
        var antiforgery = new TestAntiforgery(rejectCsrf);
        var gate = new PvadDesignerAccessMiddleware(_ => { allowed = true; return Task.CompletedTask; });
        var aliases = new PvadLegacyApiMiddleware(request =>
            gate.InvokeAsync(request, new AllowEntitlements(), db, antiforgery));
        await aliases.InvokeAsync(context);
        return (allowed, context.Response.StatusCode, antiforgery.Calls, await db.AuditEvents.CountAsync());
    }

    private sealed class AllowEntitlements : IPvadEntitlementService
    {
        public Task<PvadEntitlementDecision> EvaluateAsync(
            Guid organizationId, string capability, long quantity = 0,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PvadEntitlementDecision(
                true, "", "Allowed for test.", null, null, capability, null, null, null, 0, null));
    }

    private sealed class TestAntiforgery(bool reject) : IAntiforgery
    {
        public int Calls { get; private set; }
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext context) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext context) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext context) => Task.FromResult(!reject);
        public void SetCookieTokenAndHeader(HttpContext context) => throw new NotSupportedException();
        public Task ValidateRequestAsync(HttpContext context)
        {
            Calls++;
            if (reject) throw new AntiforgeryValidationException("Rejected for test.");
            return Task.CompletedTask;
        }
    }
}
