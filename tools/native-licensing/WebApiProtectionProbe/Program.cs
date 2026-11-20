using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PVAD.Core.Contracts;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.Licensing;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Licensing;
using PVAD.WebApi.Infrastructure;

namespace PVAD.NativeLicenseProtection;

internal static class Probe
{
    internal static readonly Guid UserId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    internal static readonly Guid OrganizationId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly List<string> Checks = [];
    internal static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("Failed protected-host check: " + name);
        if (Checks.Contains(name)) throw new InvalidOperationException("Duplicate host check: " + name);
        Checks.Add(name);
    }

    private static async Task Main(string[] args)
    {
        if (args.Length is not (5 or 6) || (args.Length == 6 && args[5] != "unlicensed")) throw new ArgumentException("Expected license directory, CodeWorker directory, host content root, result directory, revocation list and optional unlicensed mode.");
        var unlicensed = args.Length == 6;
        var licenses = args[0];
        Directory.CreateDirectory(args[3]);
        await using var factory = new ProbeFactory(args[2], unlicensed ? null : Path.Combine(licenses, "integrated.json"), args[1]);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            db.Organizations.Add(new Organization { Id = OrganizationId, Name = "Synthetic protected host", Slug = "synthetic-protected-host" });
            var subscription = new OrganizationSubscription { OrganizationId = OrganizationId, Status = SubscriptionStatus.Active, Edition = SubscriptionEdition.Premium };
            db.OrganizationSubscriptions.Add(subscription);
            foreach (var capability in new[] { "designer", "api", "generator", "migration" })
                db.SubscriptionEntitlements.Add(new SubscriptionEntitlement { SubscriptionId = subscription.Id, Capability = capability, Enabled = true });
            await db.SaveChangesAsync();
        }
        Check(unlicensed ? PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed : PvadLicense.GetStatus().CanStartOperation,
            unlicensed ? "host-starts-without-native-license" : "host-bootstrap-explicit-signed-license");
        using (var live = await client.GetAsync("/health/live")) Check(live.StatusCode == HttpStatusCode.OK, "real-host-liveness");
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/pvad/v1/auth/csrf");
        var token = csrf.GetProperty("token").GetString()!;
        const string export = "/api/export?format=pdf";
        var design = new DesignExportDto
        {
            Id = "protected-host-document", Name = "Synthetic protected host", PageSettings = new() { Width = 595, Height = 842 },
            Pages = [new() { Id = "portrait", Elements = [new() { Id = "text", Type = "text", X = 40, Y = 50, Width = 300, Height = 40, Content = "Synthetic protected host" }] },
                new() { Id = "landscape", Layout = new() { Width = 842, Height = 595, Orientation = "landscape" }, Elements = [] }],
        };
        async Task<HttpResponseMessage> Export(string? scenario = null, bool includeCsrf = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, export) { Content = JsonContent.Create(design) };
            request.Headers.Add("X-PVAD-Application", "designer");
            if (includeCsrf) request.Headers.Add("X-PVAD-CSRF", token);
            if (scenario is not null) request.Headers.Add("X-Synthetic-Scenario", scenario);
            return await client.SendAsync(request);
        }
        async Task Problem(HttpResponseMessage response, HttpStatusCode status, string code, string name)
        {
            using (response)
            {
                var body = await response.Content.ReadAsStringAsync();
                Check(response.StatusCode == status, name + ":status:" + response.StatusCode);
                Check(response.Content.Headers.ContentType?.MediaType == "application/problem+json", name + ":mime");
                var problem = JsonSerializer.Deserialize<JsonElement>(body);
                Check(problem.GetProperty("code").GetString() == code, name + ":diagnostic");
                Check(!body.Contains(licenses, StringComparison.Ordinal) && response.Content.Headers.ContentDisposition is null, name + ":no-private-path-or-download");
            }
        }
        await Problem(await Export("anonymous"), HttpStatusCode.Unauthorized, PvadApiProblems.AuthenticationRequired, "anonymous-denied");
        await Problem(await Export("no-export-permission"), HttpStatusCode.Forbidden, "PVAD_DESIGNER_PERMISSION_REQUIRED", "permission-denied");
        await Problem(await Export("other-tenant"), HttpStatusCode.Forbidden, "PVAD_ORGANIZATION_INACTIVE", "other-tenant-denied");
        await Problem(await Export(includeCsrf: false), HttpStatusCode.BadRequest, PvadApiProblems.InvalidCsrf, "csrf-denied");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            var entitlement = await db.SubscriptionEntitlements.SingleAsync(value => value.Capability == "generator");
            entitlement.Enabled = false; await db.SaveChangesAsync();
            await Problem(await Export(), HttpStatusCode.Forbidden, "PVAD_ENTITLEMENT_DENIED", "product-entitlement-denied");
            entitlement.Enabled = true; await db.SaveChangesAsync();
            var policy = new DesignerFeaturePolicy { OrganizationId = OrganizationId, FeatureId = "designer.nutrient-viewer-migration", EnabledOverride = false };
            db.DesignerFeaturePolicies.Add(policy); await db.SaveChangesAsync();
            foreach (var kind in new[] { "viewer", "designer" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/pvad/v1/migrations/pdf/{kind}/nutrient-viewer/convert")
                {
                    Content = JsonContent.Create(new { inputType = "javascript", source = "NutrientViewer.load({ document: '/synthetic.pdf' });" }),
                };
                request.Headers.Add("X-PVAD-Application", "designer"); request.Headers.Add("X-PVAD-CSRF", token);
                await Problem(await client.SendAsync(request), HttpStatusCode.Forbidden, "PVAD_DESIGNER_FEATURE_DISABLED", "organization-feature-disabled-" + kind);
            }
            db.DesignerFeaturePolicies.Remove(policy); await db.SaveChangesAsync();
        }
        if (unlicensed)
        {
            await Problem(await Export(), HttpStatusCode.ServiceUnavailable, "PVADLIC001", "unlicensed-native-denied");
            Console.WriteLine(JsonSerializer.Serialize(new { checks = Checks, actualArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
            return;
        }
        PvadLicense.LoadFromFile(Path.Combine(licenses, "word.json"));
        await Problem(await Export(), HttpStatusCode.ServiceUnavailable, "PVADLIC012", "native-word-denied");
        var previousLicense = PvadLicense.GetStatus().LicenseId;
        try { PvadLicense.LoadFromFile(Path.Combine(licenses, "expired.json")); Check(false, "expired-reload-must-fail"); }
        catch (PvadLicenseException exception) { Check(exception.Code == "PVADLIC009", "expired-reload-rejected"); }
        Check(PvadLicense.GetStatus().LicenseId == previousLicense, "expired-reload-retains-existing-snapshot");
        await Problem(await Export(), HttpStatusCode.ServiceUnavailable, "PVADLIC012", "expired-reload-retains-prior-rights");
        PvadLicense.LoadFromFile(Path.Combine(licenses, "integrated.json"));
        using (var exported = await Export())
        {
            var bytes = await exported.Content.ReadAsByteArrayAsync();
            Check(exported.StatusCode == HttpStatusCode.OK, "export-http-success:" + exported.StatusCode);
            Check(exported.Content.Headers.ContentType?.MediaType == "application/pdf", "export-pdf-mime");
            Check(bytes.AsSpan().StartsWith("%PDF-"u8), "export-pdf-signature");
            await File.WriteAllBytesAsync(Path.Combine(args[3], "export.pdf"), bytes);
        }

        PvadLicense.LoadFromFile(Path.Combine(licenses, "migration-designer.json"));
        using (var form = new MultipartFormDataContent())
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/pvad/v1/migrations/pdf/designer/nutrient-viewer/convert"))
        {
            form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(args[3], "export.pdf"))), "pdf", "synthetic.pdf");
            form.Add(new StringContent("instantJson"), "inputType");
            form.Add(new StringContent("""{"format":"https://pspdfkit.com/instant-json/v1","annotations":[{"id":"synthetic-note","type":"pspdfkit/text","pageIndex":0,"bbox":[20,30,160,24],"text":"Synthetic review"}]}"""), "source");
            request.Content = form;
            request.Headers.Add("X-PVAD-Application", "designer"); request.Headers.Add("X-PVAD-CSRF", token);
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            Check(response.StatusCode == HttpStatusCode.OK, "nutrient-designer-friend-boundary:" + response.StatusCode);
            using var result = JsonDocument.Parse(body);
            var converted = result.RootElement.GetProperty("artifacts")[0].GetProperty("content");
            Check(converted.GetProperty("pages").GetArrayLength() == 2, "nutrient-designer-two-pages");
            Check(converted.GetProperty("pages")[1].GetProperty("layout").GetProperty("width").GetDouble() == 842, "nutrient-designer-mixed-layout");
            Check(converted.GetProperty("pages")[1].GetProperty("layout").GetProperty("height").GetDouble() == 595, "nutrient-designer-landscape-height");
            Check(body.Contains("Synthetic review", StringComparison.Ordinal), "nutrient-designer-annotation-preserved");
            Check(PvadLicense.GetStatus().CanStartOperation, "nutrient-designer-admission-active");
        }
        PvadLicense.LoadFromFile(Path.Combine(licenses, "integrated.json"));

        using (var scope = factory.Services.CreateScope())
        {
            var worker = scope.ServiceProvider.GetRequiredService<IPvadCodeWorkerClient>();
            var result = await worker.RunAsync(new() { Language = PvadCodeLanguages.CSharpPdf, Operation = "execute",
                Source = "var document = new PdfDocument(); document.AddPage().DrawText(\"Synthetic protected worker\", 40, 700, 18); return document;" }, default);
            Check(result.Success, "real-codeworker-process:" + string.Join(',', result.Diagnostics.Select(value => value.Code)));
            Check(result.PdfBytes is { Length: > 5 } && result.PdfBytes.AsSpan().StartsWith("%PDF-"u8), "real-codeworker-pdf");
            await File.WriteAllBytesAsync(Path.Combine(args[3], "worker.pdf"), result.PdfBytes!);
        }

        PvadLicense.LoadFromFile(Path.Combine(licenses, "pdf.json"));
        PvadLicense.ImportRevocationsFromFile(args[4]);
        await Problem(await Export(), HttpStatusCode.ServiceUnavailable, "PVADLIC014", "revoked-native-denied");
        using (var scope = factory.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<IOptions<PvadNativeLicenseOptions>>().Value;
            var originalLicense = options.LicensePath;
            try
            {
                options.LicensePath = Path.Combine(licenses, "pdf.json");
                var denied = await scope.ServiceProvider.GetRequiredService<IPvadCodeWorkerClient>().RunAsync(new()
                {
                    Language = PvadCodeLanguages.CSharpPdf, Operation = "execute", Source = "return new PdfDocument();",
                }, default);
                Check(!denied.Success && denied.PdfBytes is null && denied.CanonicalDesign is null, "worker-host-revocation-no-result");
                Check(denied.Diagnostics.Any(value => value.Code == "PVADLIC014"), "worker-host-revocation-propagated");
            }
            finally { options.LicensePath = originalLicense; }
        }
        PvadLicense.LoadFromFile(Path.Combine(licenses, "integrated.json"));

        using (var scope = factory.Services.CreateScope())
        {
            var worker = scope.ServiceProvider.GetRequiredService<IPvadCodeWorkerClient>();
            var blocked = await worker.RunAsync(new() { Language = PvadCodeLanguages.CSharpPdf, Operation = "execute",
                Source = "return System.IO.File.ReadAllText(\"forbidden\");" }, default);
            Check(!blocked.Success && blocked.PdfBytes is null && blocked.CanonicalDesign is null, "worker-forbidden-source-no-result");
            Check(blocked.Diagnostics.Any(value => value.Severity == "error"), "worker-forbidden-source-diagnostic");
            var audit = await scope.ServiceProvider.GetRequiredService<PvadDbContext>().AuditEvents.AsNoTracking().ToListAsync();
            Check(audit.Any(value => value.Action == "security.designer-permission.denied" && value.OrganizationId == OrganizationId), "real-ef-permission-denial-audit");
            var usage = await scope.ServiceProvider.GetRequiredService<PvadDbContext>().SubscriptionUsageEvents.AsNoTracking().ToListAsync();
            Check(usage.Count > 0 && usage.All(value => value.OrganizationId == OrganizationId), "real-ef-usage-tenant-boundary");
        }
        Console.WriteLine(JsonSerializer.Serialize(new { checks = Checks, actualArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
    }
}

internal sealed class ProbeFactory(string contentRoot, string? licensePath, string codeWorker) : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing").UseContentRoot(contentRoot);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PvadDatabase"] = "Host=127.0.0.1;Port=1;Database=synthetic_no_network;Username=unused;Password=unused",
            ["NativeLicensing:LicensePath"] = licensePath,
            ["ProductAccess:Enabled"] = "true",
            ["OAuth:Enabled"] = "false",
            ["CodeWorker:Enabled"] = "true",
            ["CodeWorker:Hardened"] = "true",
            ["CodeWorker:WorkerPath"] = codeWorker,
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.AddHostedService<PvadNativeLicenseBootstrap>();
            services.PostConfigure<PvadOAuthOptions>(options => options.Enabled = false);
            services.RemoveAll<DbContextOptions<PvadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PvadDbContext>>();
            services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase("synthetic-protection-" + GetHashCode()));
            services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "synthetic-protection"; options.DefaultChallengeScheme = "synthetic-protection"; })
                .AddScheme<AuthenticationSchemeOptions, ProbeAuthentication>("synthetic-protection", _ => { });
        });
    }
}

internal sealed class ProbeAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var scenario = Request.Headers["X-Synthetic-Scenario"].ToString();
        if (scenario == "anonymous") return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Probe.UserId.ToString()),
            new(PvadClaimTypes.ActiveOrganization, (scenario == "other-tenant" ? Guid.Parse("33333333-3333-4333-8333-333333333333") : Probe.OrganizationId).ToString()),
        };
        claims.AddRange((scenario == "no-export-permission" ? ["designer.pdf.create"] : PvadDesignerPermissions.All)
            .Select(value => new Claim(PvadClaimTypes.Permission, value)));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}
