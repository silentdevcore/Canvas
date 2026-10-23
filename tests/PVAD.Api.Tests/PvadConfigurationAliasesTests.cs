using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadConfigurationAliasesTests
{
    [Theory]
    [InlineData("Pxa:EInvoices:Evidence:RetentionDays", "Pvad:EInvoices:Evidence:RetentionDays")]
    [InlineData("ConnectionStrings:PxaDatabase", "ConnectionStrings:PvadDatabase")]
    public void Reads_legacy_keys_and_preserves_neutral_keys(string legacy, string canonical)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [legacy] = "configured", ["Storage:RootPath"] = "unchanged",
        });
        PvadConfigurationAliases.Apply(configuration);
        Assert.Equal("configured", configuration[canonical]);
        Assert.Equal("unchanged", configuration["Storage:RootPath"]);
    }

    [Fact]
    public void Higher_priority_legacy_settings_override_canonical_defaults()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pvad:EInvoices:Evidence:RetentionDays"] = "3650",
        });
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pxa:EInvoices:Evidence:RetentionDays"] = "90",
        });
        PvadConfigurationAliases.Apply(configuration);
        Assert.Equal("90", configuration["Pvad:EInvoices:Evidence:RetentionDays"]);
    }

    [Fact]
    public void Conflicting_aliases_in_the_same_provider_fail_without_values()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PxaDatabase"] = "old-secret",
            ["ConnectionStrings:PvadDatabase"] = "new-secret",
        });
        var error = Assert.Throws<InvalidOperationException>(() => PvadConfigurationAliases.Apply(configuration));
        Assert.DoesNotContain("old-secret", error.Message);
        Assert.DoesNotContain("new-secret", error.Message);
    }

    [Theory]
    [InlineData("ConnectionStrings:PxaDatabase")]
    [InlineData("ConnectionStrings:PvadDatabase")]
    public void Actual_host_uses_the_selected_connection_without_opening_a_database(string key)
    {
        const string connection = "Host=synthetic.invalid;Database=rename_test;Username=synthetic";
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [key] = connection,
                    ["OAuth:Enabled"] = "false",
                })));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(connection, scope.ServiceProvider.GetRequiredService<PvadDbContext>().Database.GetConnectionString());
    }
}
