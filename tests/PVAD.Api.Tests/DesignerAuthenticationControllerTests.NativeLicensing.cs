using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PVAD.CodeWorker;
using PVAD.Core.Contracts;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Infrastructure.Persistence.Identity;
using PVAD.Licensing;
using PVAD.Testing;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;
using Testcontainers.PostgreSql;

namespace PVAD.Api.Tests;

public sealed partial class DesignerAuthenticationControllerTests
{
    [PostgreSqlFact]
    public async Task NativeLicenseHost_preserves_real_sessions_tenants_permissions_and_worker_admission()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        var previousLicense = NativeHostLicense("integrated");
        try
        {
            using var factory = CreateNativeHostFactory(postgres.GetConnectionString(), NativeHostLicense("pdf"));
            var customer = await SeedCustomerAsync(factory.Services);
            Assert.Single(factory.Services.GetServices<IHostedService>(), service => service is PvadNativeLicenseBootstrap);
            Assert.True(PvadLicense.GetStatus().CanStartOperation);
            var templateId = Guid.NewGuid();
            var foreignTemplateId = Guid.NewGuid();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
                var subscription = await db.OrganizationSubscriptions.SingleAsync();
                foreach (var capability in new[] { "api", "generator" })
                    db.SubscriptionEntitlements.Add(new() { SubscriptionId = subscription.Id, Capability = capability, Enabled = true });
                var foreign = new Organization { Name = "Synthetic foreign workspace", Slug = "native-foreign-workspace" };
                db.Organizations.Add(foreign);
                db.DesignerTemplates.AddRange(
                    CreateTemplate(templateId, customer, "Native host template", DateTimeOffset.UtcNow),
                    CreateTemplate(foreignTemplateId, new(customer.UserId, foreign.Id), "Foreign template", DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
            }

            using var account = CreateClient(factory);
            using var designer = CreateClient(factory);
            var path = $"/api/pvad/v1/designer/templates/{templateId}/code-workspace/execute";
            var operation = new { language = "csharpPdf", source = "var document = new PdfDocument(); document.AddPage().DrawText(\"Native host worker\", 40, 700, 18); return document;" };
            var expectedJobs = 0;
            async Task AssertJobsUnchanged()
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
                Assert.Equal(expectedJobs, await db.BackgroundJobs.CountAsync());
            }
            async Task Denied(HttpClient client, HttpStatusCode status, string? code, string? target = null, bool csrf = true)
            {
                using var request = csrf
                    ? await CreateCsrfRequestAsync(client, HttpMethod.Post, target ?? path, operation, designer: true)
                    : new HttpRequestMessage(HttpMethod.Post, target ?? path) { Content = JsonContent.Create(operation) };
                if (!csrf) request.Headers.Add("X-PVAD-Application", "designer");
                using var response = await client.SendAsync(request);
                Assert.Equal(status, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (code is not null) Assert.Equal(code, problem.GetProperty("code").GetString());
                Assert.False(problem.TryGetProperty("pdfBytes", out _));
                Assert.False(problem.TryGetProperty("canonicalDesign", out _));
                await AssertJobsUnchanged();
            }
            async Task<PvadCodeWorkerResponse> Execute()
            {
                using var request = await CreateCsrfRequestAsync(designer, HttpMethod.Post, path, operation, designer: true);
                using var response = await designer.SendAsync(request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                expectedJobs++;
                return (await response.Content.ReadFromJsonAsync<PvadCodeWorkerResponse>())!;
            }

            await Denied(designer, HttpStatusCode.Unauthorized, PvadApiProblems.AuthenticationRequired);
            await LoginAsync(account);
            await Denied(account, HttpStatusCode.Unauthorized, PvadApiProblems.AuthenticationRequired);
            var sessionId = await ExchangeAndFindSessionAsync(factory, designer, await CreateHandoffAsync(account, "/pdf/create?mode=code"));
            var result = await Execute();
            Assert.True(result.Success, string.Join(',', result.Diagnostics.Select(value => value.Code)));
            Assert.NotNull(result.CanonicalDesign);
            Assert.True(result.PdfBytes is { Length: > 5 } && result.PdfBytes.AsSpan().StartsWith("%PDF-"u8));
            await Denied(designer, HttpStatusCode.BadRequest, PvadApiProblems.InvalidCsrf, csrf: false);
            await Denied(designer, HttpStatusCode.NotFound, null,
                $"/api/pvad/v1/designer/templates/{foreignTemplateId}/code-workspace/execute");

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
                var policy = new DesignerFeaturePolicy { OrganizationId = customer.OrganizationId,
                    FeatureId = "designer.code-workspace", EnabledOverride = false, UpdatedByUserId = customer.UserId };
                db.DesignerFeaturePolicies.Add(policy);
                await db.SaveChangesAsync();
                await Denied(designer, HttpStatusCode.Forbidden, "PVAD_DESIGNER_FEATURE_DISABLED");
                db.DesignerFeaturePolicies.Remove(policy);
                var entitlement = await db.SubscriptionEntitlements.SingleAsync(value => value.Capability == "designer");
                entitlement.Enabled = false;
                await db.SaveChangesAsync();
                await Denied(designer, HttpStatusCode.Forbidden, "PVAD_ENTITLEMENT_DENIED");
                entitlement.Enabled = true;
                await db.SaveChangesAsync();

                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<PvadIdentityRole>>();
                var viewer = new PvadIdentityRole { Name = PvadRoles.Viewer, IsSystemRole = true };
                Assert.True((await roles.CreateAsync(viewer)).Succeeded);
                var membershipRole = await db.OrganizationMembershipRoles.SingleAsync();
                var ownerRoleId = membershipRole.RoleId;
                membershipRole.RoleId = viewer.Id;
                await db.SaveChangesAsync();
                await Denied(designer, HttpStatusCode.Forbidden, PvadApiProblems.PermissionDenied);
                membershipRole.RoleId = ownerRoleId;
                await db.SaveChangesAsync();
            }

            var options = factory.Services.GetRequiredService<IOptions<PvadNativeLicenseOptions>>().Value;
            var workerLicense = options.LicensePath;
            try
            {
                // A licensed host cannot lend its in-memory rights to a separately verified worker.
                options.LicensePath = NativeHostLicense("word");
                var denied = await Execute();
                Assert.False(denied.Success);
                Assert.Null(denied.PdfBytes);
                Assert.Null(denied.CanonicalDesign);
                Assert.Contains(denied.Diagnostics, value => value.Code == "PVADLIC012");
                Assert.True(PvadLicense.GetStatus().CanStartOperation);
            }
            finally { options.LicensePath = workerLicense; }

            var restored = await Execute();
            Assert.True(restored.Success);
            Assert.NotEmpty(restored.PdfBytes!);
            var design = new DesignExportDto { Id = "native-host-export", Name = "Native host export",
                Pages = [new() { Id = "page", Elements = [new() { Id = "text", Type = "text", X = 20, Y = 20,
                    Width = 200, Height = 30, Content = "Native host export" }] }] };
            using (var request = await CreateCsrfRequestAsync(designer, HttpMethod.Post, "/api/export?format=pdf", design, designer: true))
            using (var response = await designer.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
                Assert.True((await response.Content.ReadAsByteArrayAsync()).AsSpan().StartsWith("%PDF-"u8));
            }
            try
            {
                PvadLicense.LoadFromFile(NativeHostLicense("word"));
                using var request = await CreateCsrfRequestAsync(designer, HttpMethod.Post, "/api/export?format=pdf", design, designer: true);
                using var response = await designer.SendAsync(request);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal("PVADLIC012", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
                Assert.Null(response.Content.Headers.ContentDisposition);
            }
            finally { PvadLicense.LoadFromFile(NativeHostLicense("pdf")); }
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
                var jobs = await db.BackgroundJobs.ToArrayAsync();
                Assert.Equal(expectedJobs, jobs.Length);
                Assert.All(jobs, job => Assert.Equal(customer.OrganizationId, job.OrganizationId));
                Assert.Equal(2, jobs.Count(job => job.Status == PvadBackgroundJobStatus.Completed));
                Assert.Equal("PVADLIC012", Assert.Single(jobs, job => job.Status == PvadBackgroundJobStatus.Failed).FailureReason);
                var audit = await db.AuditEvents.Where(value => value.Action.StartsWith("designer.code-workspace.") ||
                    value.Action.StartsWith("security.designer-")).ToArrayAsync();
                Assert.NotEmpty(audit);
                Assert.All(audit, entry => Assert.Equal(customer.OrganizationId, entry.OrganizationId));
                Assert.Equal(expectedJobs, audit.Count(entry => entry.Action == "designer.code-workspace.executed"));
                Assert.Contains(audit, entry => entry.Action == "security.designer-entitlement.denied");
                Assert.All(jobs, job => Assert.DoesNotContain(operation.source, job.PayloadJson));
                Assert.All(audit, entry => Assert.DoesNotContain(operation.source, entry.DetailsJson ?? ""));
                var usage = await db.SubscriptionUsageEvents.ToArrayAsync();
                Assert.NotEmpty(usage);
                Assert.All(usage, entry => Assert.Equal(customer.OrganizationId, entry.OrganizationId));
                var session = await db.UserSessions.SingleAsync(value => value.Id == sessionId);
                session.RevokedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }
            await Denied(designer, HttpStatusCode.Unauthorized, PvadApiProblems.AuthenticationRequired);
            Assert.True(PvadLicense.GetStatus().CanStartOperation);
        }
        finally { PvadLicense.LoadFromFile(previousLicense); }
    }

    [Fact]
    public void NativeLicenseHost_invalid_explicit_license_fails_real_host_startup()
    {
        var before = PvadLicense.GetStatus();
        using var factory = CreateNativeHostFactory(
            "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1",
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "absent.json"));
        var error = Assert.Throws<PvadLicenseException>(() => factory.CreateClient());
        Assert.Equal("PVADLIC013", error.Code);
        Assert.Equal(before, PvadLicense.GetStatus());
    }

    [Fact]
    public void NativeLicenseHost_valid_native_license_does_not_bypass_worker_startup_validation()
    {
        var before = PvadLicense.GetStatus();
        using var factory = CreateNativeHostFactory(
            "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1",
            NativeHostLicense("integrated"), hardened: false);
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains(error.Failures, value => value.Contains("isolated deployment", StringComparison.Ordinal));
        Assert.Equal(before, PvadLicense.GetStatus());
    }

    private static string NativeHostLicense(string name)
    {
        var configured = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Use the signed native test runner.");
        return Path.Combine(Path.GetDirectoryName(configured)!, name + ".json");
    }

    private static WebApplicationFactory<Program> CreateNativeHostFactory(string connection, string license, bool hardened = true) =>
        CreateFactory(connection).WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NativeLicensing:LicensePath"] = license,
                ["NativeLicensing:RevocationListPath"] = null,
                ["ProductAccess:Enabled"] = "true",
                ["CodeWorker:Enabled"] = "true",
                ["CodeWorker:Hardened"] = hardened.ToString(),
                ["CodeWorker:WorkerPath"] = Path.GetDirectoryName(typeof(SandboxPolicy).Assembly.Location),
            }));
            builder.ConfigureTestServices(services => services.AddHostedService<PvadNativeLicenseBootstrap>());
        });
}
