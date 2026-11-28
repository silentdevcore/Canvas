using System.Collections;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PVAD.FileImporter;
using PVAD.Licensing;
using PVAD.Infrastructure.Persistence;
using PVAD.Domain.Entities;
using PVAD.Migration.Pdf.Viewer.Nutrient;
using PVAD.Testing;
using PVAD.WebApi.Services;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Entitlements;

namespace PVAD.Api.Tests;

[Collection("Native spreadsheet license renewal")]
public sealed class PvadNativeViewerMigrationLicenseTests
{
    private static readonly Guid ViewerUserId = Guid.NewGuid();
    private static readonly Guid ViewerOrganizationId = Guid.NewGuid();
    private const string JavaScript = "NutrientViewer.load({ document: '/synthetic.pdf', initialViewState: { currentPageIndex: 1 } });";
    private const string InstantJson = """
        {"format":"https://pspdfkit.com/instant-json/v1","annotations":[
          {"id":"synthetic-note","type":"pspdfkit/text","pageIndex":0,"bbox":[20,30,160,24],"text":"Synthetic review"}]}
        """;
    private static string Integrated => PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")
        ?? throw new InvalidOperationException("Run with a signed native test session.");
    private static void Load(string profile) => PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(Integrated)!, profile + ".json"));

    [Fact]
    public async Task Canonical_and_legacy_viewer_routes_require_only_viewer_provider_rights()
    {
        using var factory = new PvadApiTestFactory();
        var databaseName = "native-viewer-admission-" + Guid.NewGuid();
        using var application = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<PvadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PvadDbContext>>();
            services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.RemoveAll<IPvadEntitlementService>();
            services.AddSingleton<IPvadEntitlementService, ViewerEntitlements>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "synthetic-native-viewer";
                options.DefaultChallengeScheme = "synthetic-native-viewer";
            }).AddScheme<AuthenticationSchemeOptions, ViewerAuthentication>("synthetic-native-viewer", _ => { });
        }));
        using var client = application.CreateClient();
        using (var scope = application.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            var subscription = new OrganizationSubscription
            {
                OrganizationId = ViewerOrganizationId, AccountType = SubscriptionAccountType.Company, Status = SubscriptionStatus.Active,
            };
            db.OrganizationSubscriptions.Add(subscription);
            db.SubscriptionEntitlements.Add(new SubscriptionEntitlement
            {
                SubscriptionId = subscription.Id, Capability = "migration.pdf.viewer", Enabled = true, Source = EntitlementSource.NegotiatedOverride,
            });
            await db.SaveChangesAsync();
        }
        try
        {
            foreach (var profile in new[] { "pdf", "importer", "migration-code", "migration-designer", "migration-viewer" })
            {
                Load(profile);
                foreach (var kind in new[] { "javascript", "typescript", "instantJson" })
                {
                    var source = kind == "instantJson" ? InstantJson : JavaScript;
                    foreach (var route in new[] { "/api/pvad/v1/migrations/pdf/viewer/nutrient-viewer/convert", "/api/pvad/v1/migrations/pdf/viewer/convert" })
                    {
                        using var response = await client.PostAsJsonAsync(route, new
                        {
                            inputType = kind, source, transpiledSource = JavaScript,
                            pages = new[] { new NutrientPageGeometry(595, 842) },
                        });
                        if (profile == "migration-viewer") Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        else await Denied(response);
                    }
                }
            }
            foreach (var product in new[] { "pdf", "word", "spreadsheet", "importer", "converter", "migration.pdf.designer", "migration.pdf.code" })
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => PvadLicense.Require(product, product.StartsWith("migration.") ? "nutrient-viewer" : null)).Code);
            foreach (var (header, status) in new[] { ("X-Test-Anonymous", HttpStatusCode.Unauthorized), ("X-Test-No-Permissions", HttpStatusCode.Forbidden) })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pvad/v1/migrations/pdf/viewer/convert")
                {
                    Content = JsonContent.Create(new { inputType = "javascript", source = JavaScript }),
                };
                request.Headers.Add(header, "true");
                using var response = await client.SendAsync(request);
                Assert.Equal(status, response.StatusCode);
            }
        }
        finally { PvadLicense.LoadFromFile(Integrated); }
    }

    [Fact]
    public async Task Designer_only_license_includes_source_pdf_and_mapping_but_not_public_engines()
    {
        using var factory = new PvadApiTestFactory();
        using var client = factory.CreateCompanyProductClient();
        var pdf = CreatePdf();
        try
        {
            foreach (var profile in new[] { "pdf", "importer", "migration-code", "migration-viewer", "migration-designer" })
            {
                Load(profile);
                foreach (var kind in new[] { "instantJson", "javascript", "typescript" })
                {
                    using var form = new MultipartFormDataContent();
                    form.Add(new ByteArrayContent(pdf), "pdf", "synthetic.pdf");
                    form.Add(new StringContent(kind), "inputType");
                    form.Add(new StringContent(kind == "instantJson" ? InstantJson : JavaScript), "source");
                    form.Add(new StringContent(JavaScript), "transpiledSource");
                    using var response = await client.PostAsync("/api/pvad/v1/migrations/pdf/designer/nutrient-viewer/convert", form);
                    if (profile != "migration-designer") { await Denied(response); continue; }
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var design = result.RootElement.GetProperty("artifacts")[0].GetProperty("content");
                    Assert.Equal(2, design.GetProperty("pages").GetArrayLength());
                    Assert.Equal(842, design.GetProperty("pages")[1].GetProperty("layout").GetProperty("width").GetDouble());
                    if (kind == "instantJson") Assert.Contains("Synthetic review", design.GetRawText());
                }
            }
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => new NutrientViewerSourceConverter().Convert(JavaScript)).Code);
            using var stream = new MemoryStream(pdf);
            Assert.Equal("PVADLIC012", (await Assert.ThrowsAsync<PvadLicenseException>(() => PdfFileImporter.DoImportAsync(stream))).Code);
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => PvadLicense.Require("pdf")).Code);
            PvadLicense.LoadFromFile(Integrated);
            using var renewed = new MemoryStream(pdf);
            Assert.Equal(2, (await PdfFileImporter.DoImportAsync(renewed)).Pages.Count);
        }
        finally { PvadLicense.LoadFromFile(Integrated); }
    }

    [Fact]
    public async Task Admitted_designer_finishes_after_source_callback_replacement_without_granting_reentrant_work()
    {
        var pdf = CreatePdf();
        var service = new NutrientViewerDesignMigrationService();
        try
        {
            Load("migration-designer");
            using var baselineStream = new MemoryStream(pdf);
            var baseline = await service.ConvertAsync(baselineStream, "synthetic.pdf", "instantJson", InstantJson, null, null, null, CancellationToken.None);
            using var changed = new CallbackStream(pdf, () =>
            {
                Load("pdf");
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => new NutrientViewerSourceConverter().Convert(JavaScript)).Code);
                using var nested = new MemoryStream(pdf);
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => PdfFileImporter.DoImportAsync(nested).GetAwaiter().GetResult()).Code);
                using var designerInput = new MemoryStream(pdf);
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => service.ConvertAsync(designerInput, "synthetic.pdf", "instantJson", InstantJson, null, null, null, CancellationToken.None).GetAwaiter().GetResult()).Code);
            });
            var result = await service.ConvertAsync(changed, "synthetic.pdf", "instantJson", InstantJson, null, null, null, CancellationToken.None);
            Assert.Equal(1, changed.Calls);
            Assert.Equal(ComparableDesign(baseline.Design), ComparableDesign(result.Design));
            using var fresh = new MemoryStream(pdf);
            Assert.Equal("PVADLIC012", (await Assert.ThrowsAsync<PvadLicenseException>(() => service.ConvertAsync(fresh, "synthetic.pdf", "javascript", JavaScript, null, null, null, CancellationToken.None))).Code);

            Load("migration-designer");
            var callbackError = new IOException("Synthetic source callback failure.");
            using var failed = new CallbackStream(pdf, () => throw callbackError);
            Assert.Same(callbackError, await Assert.ThrowsAsync<IOException>(() => service.ConvertAsync(failed, "synthetic.pdf", "javascript", JavaScript, null, null, null, CancellationToken.None)));
            using var restored = new MemoryStream(pdf);
            Assert.Equal(2, (await service.ConvertAsync(restored, "synthetic.pdf", "javascript", JavaScript, null, null, null, CancellationToken.None)).Design.Pages.Count);
        }
        finally { PvadLicense.LoadFromFile(Integrated); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypeScript_callback_cannot_turn_viewer_and_designer_admissions_into_public_grants(bool designer)
    {
        var pdf = CreatePdf();
        var callback = new CallbackList(() =>
        {
            Load("pdf");
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => new NutrientViewerSourceConverter().Convert(JavaScript)).Code);
        });
        try
        {
            Load(designer ? "migration-designer" : "migration-viewer");
            if (designer)
            {
                using var stream = new MemoryStream(pdf);
                var result = await new NutrientViewerDesignMigrationService().ConvertAsync(stream, "synthetic.pdf", "typescript", JavaScript, null, JavaScript, callback, CancellationToken.None);
                Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB008");
            }
            else
            {
                var result = new NutrientViewerSourceConverter().ConvertTranspiledTypeScript(JavaScript, JavaScript, compilerDiagnostics: callback);
                Assert.Contains(result.Diagnostics, item => item.Code == "CANMIGNUTWEB008");
            }
            Assert.Equal(1, callback.Calls);
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => new NutrientInstantJsonConverter().Convert(InstantJson, [new(595, 842)])).Code);
        }
        finally { PvadLicense.LoadFromFile(Integrated); }
    }

    private static byte[] CreatePdf()
    {
#pragma warning disable PVAD0001
        var document = new PVAD.Pdf.PdfDocument();
#pragma warning restore PVAD0001
        document.AddPage(595, 842).DrawText("Synthetic portrait", 40, 350);
        document.AddPage(842, 595).DrawText("Synthetic landscape", 40, 300);
        return document.ToBytes();
    }

    private static string ComparableDesign(PVAD.Core.Contracts.DesignExportDto design)
    {
        var root = JsonSerializer.SerializeToNode(design)!;
        Assert.Matches("^[a-f0-9]{12}$", root[nameof(design.Id)]!.GetValue<string>());
        root[nameof(design.Id)] = "validated-new-import-id";
        var sidecar = root["nutrientViewerMigration"]!["annotationSidecar"]!.AsObject();
        Assert.True(DateTimeOffset.TryParse(sidecar["exportedAt"]!.GetValue<string>(), out var exported));
        Assert.InRange(exported, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        sidecar["exportedAt"] = DateTimeOffset.UnixEpoch;
        return root.ToJsonString();
    }

    private static async Task Denied(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"{response.RequestMessage?.RequestUri?.AbsolutePath}: expected a native license problem, got {response.StatusCode}. {await response.Content.ReadAsStringAsync()}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Content.Headers.ContentDisposition);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PVADLIC012", problem.RootElement.GetProperty("code").GetString());
    }

    private sealed class CallbackStream(byte[] bytes, Action callback) : MemoryStream(bytes)
    {
        public int Calls { get; private set; }
        public override bool CanSeek => false;
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            Calls++;
            callback();
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }

    private sealed class CallbackList(Action callback) : IReadOnlyList<string>
    {
        public int Calls { get; private set; }
        public int Count => 1;
        public string this[int index] => index == 0 ? "Synthetic compiler warning" : throw new IndexOutOfRangeException();
        public IEnumerator<string> GetEnumerator() { Calls++; callback(); yield return this[0]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ViewerAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Test-Anonymous")) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, ViewerUserId.ToString()),
                new(PvadClaimTypes.ActiveOrganization, ViewerOrganizationId.ToString()),
            };
            if (!Request.Headers.ContainsKey("X-Test-No-Permissions"))
                claims.AddRange(PvadDesignerPermissions.All.Select(permission => new Claim(PvadClaimTypes.Permission, permission)));
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class ViewerEntitlements : IPvadEntitlementService
    {
        public Task<PvadEntitlementDecision> EvaluateAsync(Guid organizationId, string capability, long quantity = 0,
            CancellationToken cancellationToken = default) => Task.FromResult(new PvadEntitlementDecision(
                organizationId == ViewerOrganizationId && capability == "designer", "", "Synthetic Viewer feature entitlement.",
                null, null, capability, null, null, null, 0, null));
    }
}
