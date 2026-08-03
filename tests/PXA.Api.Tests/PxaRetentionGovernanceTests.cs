using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PXA.WebApi.Application.Retention;

namespace PXA.Api.Tests;

public sealed class PxaRetentionGovernanceTests
{
    [Fact]
    public async Task Current_catalog_blocks_production_but_remains_testable_outside_production()
    {
        var catalog = new PxaRetentionPolicyCatalog();

        Assert.False(catalog.ProductionApproved);
        Assert.False(catalog.IsProductionReady);
        Assert.Equal(13, catalog.Policies.Count);
        Assert.Equal(12, catalog.Policies.Count(value => value.ApprovalStatus != "approved"));

        var productionGate = new PxaRetentionStartupGate(
            catalog,
            new TestHostEnvironment(Environments.Production),
            NullLogger<PxaRetentionStartupGate>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            productionGate.StartAsync(CancellationToken.None));

        var developmentGate = new PxaRetentionStartupGate(
            catalog,
            new TestHostEnvironment(Environments.Development),
            NullLogger<PxaRetentionStartupGate>.Instance);
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
