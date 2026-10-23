using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Application.Retention;
using PVAD.WebApi.Services.Jobs;
using PVAD.WebApi.Services.Mail;

namespace PVAD.Api.Tests;

public sealed class PvadRetentionGovernanceTests
{
    [Theory]
    [InlineData("PVAD")]
    [InlineData("PXA")]
    [InlineData("both")]
    [InlineData("defaults")]
    public async Task Status_reports_canonical_retention_keys_and_reads_both_input_generations(string input)
    {
        using var db = CreateDatabase();
        var values = new Dictionary<string, string?>();
        var suffixes = new[] { "METRIC_RETENTION", "LOG_RETENTION", "TRACE_RETENTION", "ALERT_RETENTION" };
        var expected = new[] { "31d", "62h", "93h", "124h" };
        for (var index = 0; index < suffixes.Length; index++)
        {
            if (input is "PVAD" or "both") values["PVAD_" + suffixes[index]] = expected[index];
            if (input is "PXA" or "both") values["PXA_" + suffixes[index]] = expected[index];
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var status = await CreateService(db, configuration).GetStatusAsync(CancellationToken.None);
        var policy = Assert.Single(status.Policies, value => value.Id == "product-observability");
        Assert.Equal(suffixes.Select(suffix => "PVAD_" + suffix), policy.EffectiveConfiguration.Select(value => value.Key));
        Assert.Equal(input == "defaults" ? ["90d", "720h", "336h", "120h"] : expected,
            policy.EffectiveConfiguration.Select(value => value.Value));
        Assert.False(status.ProductionReady);
        Assert.False(status.ProductionApproved);
    }

    [Fact]
    public async Task Status_rejects_conflicting_retention_values_without_disclosing_them()
    {
        using var db = CreateDatabase();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PVAD_METRIC_RETENTION"] = "current-private-value",
            ["PXA_METRIC_RETENTION"] = "legacy-private-value",
        }).Build();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(db, configuration).GetStatusAsync(CancellationToken.None));
        Assert.Contains("PVAD_METRIC_RETENTION", error.Message);
        Assert.Contains("PXA_METRIC_RETENTION", error.Message);
        Assert.DoesNotContain("current-private-value", error.Message);
        Assert.DoesNotContain("legacy-private-value", error.Message);
    }

    private static PvadDbContext CreateDatabase() => new(new DbContextOptionsBuilder<PvadDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static PvadRetentionGovernanceService CreateService(PvadDbContext db, IConfiguration configuration) =>
        new(db, new PvadRetentionPolicyCatalog(), new PvadRetentionLegalHoldService(db),
            Options.Create(new PvadJobOptions()), Options.Create(new PvadMailOptions()), configuration);

    [Fact]
    public async Task Current_catalog_blocks_production_but_remains_testable_outside_production()
    {
        var catalog = new PvadRetentionPolicyCatalog();

        Assert.False(catalog.ProductionApproved);
        Assert.False(catalog.IsProductionReady);
        Assert.Equal(13, catalog.Policies.Count);
        Assert.Equal(12, catalog.Policies.Count(value => value.ApprovalStatus != "approved"));

        var productionGate = new PvadRetentionStartupGate(
            catalog,
            new TestHostEnvironment(Environments.Production),
            NullLogger<PvadRetentionStartupGate>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            productionGate.StartAsync(CancellationToken.None));

        var developmentGate = new PvadRetentionStartupGate(
            catalog,
            new TestHostEnvironment(Environments.Development),
            NullLogger<PvadRetentionStartupGate>.Instance);
        await developmentGate.StartAsync(CancellationToken.None);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "PXA.Retention.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
