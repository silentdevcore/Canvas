using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PVAD.Licensing;
using PVAD.Pdf;
using PVAD.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Jobs;
using PVAD.WebApi.Services.Pdf;

namespace PVAD.Api.Tests;

[Collection("Native importer license renewal")]
public sealed class PvadNativeTrustLicenseTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    [Fact]
    public async Task Denied_job_and_audit_key_loading_fail_before_storage_or_file_access()
    {
        var integrated = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Run with a signed native test session.");
        try
        {
            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "pdf.json"));
            var job = new PvadBackgroundJob { Type = PvadJobQueue.PdfTrustInspectionType, InputObjectId = Guid.NewGuid(), PayloadJson = "{}" };
            var executor = new PdfTrustInspectionJobExecutor(null!, null!, null!, null!);
            var error = await Assert.ThrowsAsync<PvadLicenseException>(() => executor.ExecuteAsync(job, CancellationToken.None));
            Assert.Equal("PVADLIC012", error.Code);
            Assert.Null(job.ResultObjectId);
            Assert.Null(job.DiagnosticsJson);
            var keyError = Assert.Throws<PvadLicenseException>(() => new PvadRedactionAuditKeyProvider(
                Options.Create(new PvadRedactionAuditKeyOptions { Pkcs12Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pfx") }),
                NullLogger<PvadRedactionAuditKeyProvider>.Instance));
            Assert.Equal("PVADLIC012", keyError.Code);
            using var unconfigured = new PvadRedactionAuditKeyProvider(
                Options.Create(new PvadRedactionAuditKeyOptions()), NullLogger<PvadRedactionAuditKeyProvider>.Instance);
            Assert.Null(unconfigured.Key);
        }
        finally { PvadLicense.LoadFromFile(integrated); }
    }

    [Fact]
    public async Task Trust_routes_reject_missing_importer_rights_and_recover_after_renewal()
    {
        var integrated = PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
            ?? throw new InvalidOperationException("Run with a signed native test session.");
        using var factory = new PvadApiTestFactory();
        var databaseName = "native-trust-admission-" + Guid.NewGuid();
        using var application = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<PvadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PvadDbContext>>();
            services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "synthetic-native-trust";
                options.DefaultChallengeScheme = "synthetic-native-trust";
            }).AddScheme<AuthenticationSchemeOptions, SyntheticAuthentication>("synthetic-native-trust", _ => { });
            services.RemoveAll<IClaimsTransformation>();
            services.AddSingleton<IClaimsTransformation, SyntheticClaims>();
        }));
        using var client = application.CreateClient();
        using (var scope = application.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            database.UserSessions.Add(new UserSession
            {
                Id = SessionId, UserId = UserId, OrganizationId = OrganizationId,
                IpAddressHash = "synthetic", UserAgent = "native-admission-test", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            });
            await database.SaveChangesAsync();
        }
        var document = new PdfDocument();
        document.AddPage().DrawText("Synthetic trust admission", 40, 40);
        var bytes = document.ToBytes();
        var routes = new[]
        {
            "trust/inspect", "trust/inspect/jobs", "compliance/validate", "compliance/preflight", "compliance/convert",
            "signatures/sign", "signatures/timestamp", "signatures/validate", "redactions/preflight", "redactions/apply",
            "e-invoices/inspect", "e-invoices/extract", "e-invoices/attach/preflight", "e-invoices/attach"
        };
        try
        {
            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "pdf.json"));
            foreach (var (header, expected) in new[] { ("X-Test-Anonymous", HttpStatusCode.Unauthorized), ("X-Test-No-Permissions", HttpStatusCode.Forbidden) })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pvad/v1/pdf/trust/inspect") { Content = Upload(bytes, "trust/inspect") };
                request.Headers.Add(header, "true");
                using var denied = await client.SendAsync(request);
                Assert.Equal(expected, denied.StatusCode);
            }
            foreach (var route in routes)
            {
                using var form = Upload(bytes, route);
                using var response = await client.PostAsync("/api/pvad/v1/pdf/" + route, form);
                Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable,
                    $"{route}: {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                Assert.Null(response.Content.Headers.ContentDisposition);
                using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal("PVADLIC012", problem.RootElement.GetProperty("code").GetString());
            }
            using (var auditResponse = await client.GetAsync("/api/pvad/v1/pdf/redactions/audits/" + new string('a', 64)))
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, auditResponse.StatusCode);
                using var problem = JsonDocument.Parse(await auditResponse.Content.ReadAsStringAsync());
                Assert.Equal("PVADLIC012", problem.RootElement.GetProperty("code").GetString());
            }

            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(integrated)!, "importer.json"));
            foreach (var route in new[] { "trust/inspect", "compliance/validate", "compliance/preflight", "signatures/validate", "redactions/preflight", "e-invoices/inspect" })
            {
                using var form = Upload(bytes, route);
                using var response = await client.PostAsync("/api/pvad/v1/pdf/" + route, form);
                Assert.True(response.IsSuccessStatusCode,
                    $"{route}: {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }
        }
        finally { PvadLicense.LoadFromFile(integrated); }
    }

    private static MultipartFormDataContent Upload(byte[] pdf, string route)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(pdf), route is "signatures/sign" or "signatures/timestamp" ? "pdf" : "file", "synthetic.pdf");
        if (route == "signatures/sign")
            form.Add(new ByteArrayContent([1, 2, 3]), "certificate", "synthetic.pfx");
        if (route.StartsWith("redactions/", StringComparison.Ordinal))
            form.Add(new StringContent("[{\"pageNumber\":1,\"bounds\":{\"x\":0,\"y\":0,\"width\":300,\"height\":180}}]"), "regions");
        if (route.StartsWith("e-invoices/attach", StringComparison.Ordinal))
        {
            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("<Invoice/>")), "invoice", "synthetic.xml");
            form.Add(new StringContent("true"), "preserveUnrecognized");
        }
        if (route == "e-invoices/extract") form.Add(new StringContent("synthetic.xml"), "attachmentName");
        return form;
    }

    private sealed class SyntheticClaims : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) => Task.FromResult(principal);
    }

    private sealed class SyntheticAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Test-Anonymous")) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, UserId.ToString()), new(PvadClaimTypes.ActiveOrganization, OrganizationId.ToString()),
                new(PvadClaimTypes.Session, SessionId.ToString())
            };
            if (!Request.Headers.ContainsKey("X-Test-No-Permissions"))
                claims.AddRange(PvadDesignerPermissions.All.Select(permission => new Claim(PvadClaimTypes.Permission, permission)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
