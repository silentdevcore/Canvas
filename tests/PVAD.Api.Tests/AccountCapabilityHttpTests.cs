using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class AccountCapabilityHttpTests
{
    [Fact]
    public async Task FullHttpPipelineEnforcesPrivateFormatsWithoutDesignerHeaderAndFiltersFormatDiscovery()
    {
        var organizationId = Guid.NewGuid();
        using var factory = new PvadApiTestFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<PvadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PvadDbContext>>();
            var database = "account-http-" + Guid.NewGuid().ToString("N");
            services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase(database));
            services.AddSingleton(new TestIdentity(organizationId));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "AccountCapabilityTest";
                options.DefaultChallengeScheme = "AccountCapabilityTest";
            }).AddScheme<AuthenticationSchemeOptions, AccountAuthentication>("AccountCapabilityTest", _ => { });
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            db.Organizations.Add(new Organization { Id = organizationId, Name = "Synthetic", Slug = "synthetic" });
            db.OrganizationSubscriptions.Add(new OrganizationSubscription { OrganizationId = organizationId,
                AccountType = SubscriptionAccountType.IndividualDeveloper, Status = SubscriptionStatus.Active });
            await db.SaveChangesAsync();
        }
        var formats = await client.GetFromJsonAsync<JsonElement[]>("/api/export/formats");
        Assert.NotNull(formats);
        var keys = formats.Select(format => format.GetProperty("key").GetString()).ToArray();
        Assert.Contains("odt", keys);
        Assert.Contains("pdf", keys);
        Assert.DoesNotContain("png", keys);
        Assert.DoesNotContain("csv", keys);
        using var unknownFormat = await client.PostAsJsonAsync("/api/export?format=unknown", new { name = "synthetic", pages = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unknownFormat.StatusCode);
        var unknownProblem = await unknownFormat.Content.ReadFromJsonAsync<JsonElement>();
        var suggestions = unknownProblem.GetProperty("supportedFormats").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains("odt", suggestions);
        Assert.DoesNotContain("png", suggestions);
        using var pdfImport = await client.PostAsync("/api/document/import-pdf-engine", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.Forbidden, pdfImport.StatusCode);
        using var png = await client.PostAsJsonAsync("/api/export?format=png", new { name = "synthetic", pages = Array.Empty<object>(), sharedElements = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Forbidden, png.StatusCode);
        using var migration = await client.PostAsJsonAsync("/api/pvad/v1/migrations/pdf/code/aspose-pdf/convert", new { });
        Assert.Equal(HttpStatusCode.Forbidden, migration.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
            var subscription = await db.OrganizationSubscriptions.SingleAsync();
            subscription.AccountType = SubscriptionAccountType.Company;
            await db.SaveChangesAsync();
        }
        var companyFormats = await client.GetFromJsonAsync<JsonElement[]>("/api/export/formats");
        Assert.Contains(companyFormats!, format => format.GetProperty("key").GetString() == "png");
        using var unapprovedCompanyMigration = await client.PostAsJsonAsync("/api/pvad/v1/migrations/pdf/code/aspose-pdf/convert", new { });
        Assert.Equal(HttpStatusCode.Forbidden, unapprovedCompanyMigration.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        using var anonymous = await client.PostAsJsonAsync("/api/export?format=pdf", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private sealed record TestIdentity(Guid OrganizationId);

    private sealed class AccountAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, TestIdentity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Test-Anonymous")) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "36be46be-ef22-492b-b2b2-04ad788a285d"),
                new(PvadClaimTypes.ActiveOrganization, identity.OrganizationId.ToString()),
                new(ClaimTypes.Role, PvadRoles.SystemAdministrator),
            };
            claims.AddRange(PvadDesignerPermissions.All.Select(permission => new Claim(PvadClaimTypes.Permission, permission)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
