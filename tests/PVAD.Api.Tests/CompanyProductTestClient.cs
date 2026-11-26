using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
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

// Explicit opt-in for product behavior tests. Authentication/account denial tests
// continue to construct their own identities and account grants.
internal static class CompanyProductTestClient
{
    internal static HttpClient CreateCompanyProductClient(this WebApplicationFactory<Program> source, bool keepHostedServices = false)
    {
        var organizationId = Guid.NewGuid();
        var database = "company-product-" + Guid.NewGuid().ToString("N");
        var factory = source.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            if (!keepHostedServices) services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<PvadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PvadDbContext>>();
            services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase(database));
            services.AddSingleton(new Identity(organizationId));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "CompanyProductTest";
                options.DefaultChallengeScheme = "CompanyProductTest";
            }).AddScheme<AuthenticationSchemeOptions, Authentication>("CompanyProductTest", _ => { });
        }));
        var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PvadDbContext>();
        db.Organizations.Add(new Organization { Id = organizationId, Name = "Synthetic company", Slug = organizationId.ToString() });
        var subscription = new OrganizationSubscription
        {
            OrganizationId = organizationId, AccountType = SubscriptionAccountType.Company, Status = SubscriptionStatus.Active,
        };
        db.OrganizationSubscriptions.Add(subscription);
        db.SubscriptionEntitlements.Add(new SubscriptionEntitlement
        {
            SubscriptionId = subscription.Id, Capability = "migration", Enabled = true, Source = EntitlementSource.NegotiatedOverride,
        });
        db.SubscriptionEntitlements.Add(new SubscriptionEntitlement
        {
            SubscriptionId = subscription.Id, Capability = "designer", Enabled = true, Source = EntitlementSource.EditionDefault,
        });
        db.SaveChanges();
        return client;
    }

    private sealed record Identity(Guid OrganizationId);
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, Identity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "70f7836a-b8d5-47ae-a617-7d368f4dc9df"),
                new(PvadClaimTypes.ActiveOrganization, identity.OrganizationId.ToString()),
                new(ClaimTypes.Role, PvadRoles.SystemAdministrator),
            };
            claims.AddRange(PvadDesignerPermissions.All.Select(permission => new Claim(PvadClaimTypes.Permission, permission)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
