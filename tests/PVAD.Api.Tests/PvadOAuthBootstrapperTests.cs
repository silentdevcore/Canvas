using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadOAuthBootstrapperTests
{
    [Theory]
    [InlineData("Testing", true)]
    [InlineData("Production", false)]
    public async Task Hosts_without_automatic_bootstrap_do_not_resolve_a_database(string environment, bool enabled)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Services.Configure<PvadOAuthOptions>(options => options.Enabled = enabled);
        await using var app = builder.Build();

        await PvadOAuthBootstrapper.InitializeAsync(app);
    }

    [Fact]
    public async Task Production_with_enabled_OAuth_still_requires_its_persistent_store()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Services.Configure<PvadOAuthOptions>(options => options.Enabled = true);
        await using var app = builder.Build();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => PvadOAuthBootstrapper.InitializeAsync(app));
        Assert.Contains("PvadDbContext", error.Message);
    }
}
