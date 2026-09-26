using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.Api.Tests;

public sealed class PxaPdfTrustSecurityTests
{
    [Theory]
    [InlineData("/api/pxa/v1/pdf/trust/inspect", PxaPdfTrustCapabilities.Inspection)]
    [InlineData("/api/pxa/v1/pdf/signatures/sign", PxaPdfTrustCapabilities.Signing)]
    [InlineData("/api/pxa/v1/pdf/redactions/apply", PxaPdfTrustCapabilities.Redaction)]
    [InlineData("/api/pxa/v1/pdf/compliance/convert", PxaPdfTrustCapabilities.Compliance)]
    [InlineData("/api/pxa/v1/pdf/e-invoices/create", "generator")]
    public void Product_capabilities_are_independent_from_role_permissions(string path, string expected)
    {
        Assert.Equal(expected, PxaProductAccessMiddleware.ResolveCapability(path));
        Assert.DoesNotContain(expected, PxaDesignerPermissions.All);
    }

    [Fact]
    public async Task Disabled_trust_feature_fails_closed_with_stable_diagnostic()
    {
        var nextWasCalled = false;
        var middleware = new PxaPdfTrustAvailabilityMiddleware(
            _ => { nextWasCalled = true; return Task.CompletedTask; },
            Options.Create(new PxaPdfTrustRuntimeOptions { SigningEnabled = false }));
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/pxa/v1/pdf/signatures/sign";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.False(nextWasCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var response = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("PXAPDFTRUSTAPI901", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recent_browser_session_satisfies_step_up()
    {
        var now = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var session = CreateSession(now.AddMinutes(-5), now.AddHours(1));
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var authorization = await AuthorizeAsync(dbContext, session.Id, now);

        Assert.True(authorization.HasSucceeded);
    }

    [Fact]
    public async Task Expired_step_up_window_is_denied()
    {
        var now = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var session = CreateSession(now.AddMinutes(-16), now.AddHours(1));
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var authorization = await AuthorizeAsync(dbContext, session.Id, now);

        Assert.False(authorization.HasSucceeded);
    }

    [Fact]
    public async Task Service_accounts_cannot_bypass_interactive_step_up()
    {
        var now = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
        await using var dbContext = CreateContext();
        var session = CreateSession(now, now.AddHours(1));
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var authorization = await AuthorizeAsync(dbContext, session.Id, now, serviceAccount: true);

        Assert.False(authorization.HasSucceeded);
    }

    [Fact]
    public async Task Trust_audit_writer_persists_only_bounded_operation_evidence()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trust-test-trace" };
        var writer = new PxaPdfTrustAuditWriter(
            dbContext,
            new TestTenantContext(userId, organizationId),
            new HttpContextAccessor { HttpContext = httpContext });

        await writer.WriteAsync(new PxaPdfTrustAuditEntry(
            "pdf.signature.created",
            "BaselineB",
            new string('a', 64),
            new string('b', 64),
            "succeeded",
            ["PXAPDFTEST001"],
            "certificate-thumbprint"));

        var audit = Assert.Single(dbContext.AuditEvents.Local);
        Assert.Equal(organizationId, audit.OrganizationId);
        Assert.Equal(userId, audit.ActorUserId);
        Assert.Equal("pdf-trust-operation", audit.TargetType);
        Assert.Contains("certificate-thumbprint", audit.DetailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("password", audit.DetailsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content", audit.DetailsJson, StringComparison.OrdinalIgnoreCase);

        var operation = Assert.Single(dbContext.PdfTrustOperationJobs.Local);
        Assert.Equal(audit.Id, operation.AuditEventId);
        Assert.Equal("pdf.signature.created", operation.Operation);
        Assert.Equal(new string('a', 64), operation.InputSha256);
        Assert.Equal(new string('b', 64), operation.OutputSha256);
        Assert.Equal("certificate-thumbprint", operation.KeyIdentifier);
        Assert.Equal("trust-test-trace", operation.TraceId);
        Assert.Contains(operation.Id.ToString(), audit.DetailsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Trust_operation_jobs_are_immutable()
    {
        await using var dbContext = CreateContext();
        var operation = new PdfTrustOperationJob
        {
            OrganizationId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            AuditEventId = Guid.NewGuid(),
            Operation = "pdf.signature.created",
            Policy = "BaselineB",
            InputSha256 = new string('a', 64),
            OutputSha256 = new string('b', 64),
            Outcome = "succeeded",
            DiagnosticCodesJson = "[]",
        };
        dbContext.PdfTrustOperationJobs.Add(operation);
        await dbContext.SaveChangesAsync();

        operation.Outcome = "changed";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dbContext.SaveChangesAsync());
        Assert.Contains("immutable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(
        PxaDbContext dbContext,
        Guid sessionId,
        DateTimeOffset now,
        bool serviceAccount = false)
    {
        var claims = new List<Claim> { new(PxaClaimTypes.Session, sessionId.ToString()) };
        if (serviceAccount)
            claims.Add(new Claim(PxaClaimTypes.ServiceAccount, Guid.NewGuid().ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var requirement = new PxaRecentAuthenticationRequirement();
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        var handler = new PxaRecentAuthenticationHandler(
            dbContext,
            Options.Create(new PxaStepUpOptions { MaximumAuthenticationAgeMinutes = 15 }),
            new FixedTimeProvider(now));

        await handler.HandleAsync(context);
        return context;
    }

    private static UserSession CreateSession(DateTimeOffset createdAt, DateTimeOffset expiresAt) => new()
    {
        UserId = Guid.NewGuid(),
        IpAddressHash = new string('a', 64),
        UserAgent = "PXA security test",
        CreatedAt = createdAt,
        LastSeenAt = createdAt,
        ExpiresAt = expiresAt,
    };

    private static PxaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase($"pdf-trust-security-{Guid.NewGuid():N}")
            .Options;
        return new PxaDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestTenantContext(Guid userId, Guid organizationId) : IPxaTenantContext
    {
        public Guid? UserId { get; } = userId;
        public Guid? OrganizationId { get; } = organizationId;
    }
}
