using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadApiTestFactory : WebApplicationFactory<Program>
{
    public HttpClient CreateOpenApiClient() => WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.PostConfigure<PvadOAuthOptions>(options => options.Enabled = false);
            // The startup re-protection check needs a database; an empty one has nothing to convert.
            services.RemoveAll<DbContextOptions<PvadDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PvadDbContext>>();
            services.AddDbContext<PvadDbContext>(options => options.UseInMemoryDatabase($"openapi-{Guid.NewGuid():N}"));
        });
    }).CreateClient();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PvadDatabase"] =
                    "Host=127.0.0.1;Port=1;Database=isolated_stateless_tests;Username=unused;Password=unused",
            }));
    }
}
