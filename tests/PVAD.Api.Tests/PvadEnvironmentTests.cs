using PVAD.Infrastructure.Persistence.Configuration;

namespace PVAD.Api.Tests;

public sealed class PvadEnvironmentTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("configured", null, "configured")]
    [InlineData(null, "configured", "configured")]
    [InlineData("configured", "configured", "configured")]
    [InlineData("", null, "")]
    public void Resolves_canonical_and_legacy_values(string? current, string? legacy, string? expected)
    {
        var result = PvadEnvironment.Read("PVAD_API_KEY", key => key switch
        {
            "PVAD_API_KEY" => current,
            "PXA_API_KEY" => legacy,
            _ => throw new InvalidOperationException("Unexpected key"),
        });
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Rejects_conflicts_without_disclosing_configuration_values()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            PvadEnvironment.Read("PVAD_API_KEY", key =>
                key == "PVAD_API_KEY" ? "new-secret" : "old-secret"));
        Assert.Contains("PVAD_API_KEY and PXA_API_KEY", error.Message);
        Assert.DoesNotContain("new-secret", error.Message);
        Assert.DoesNotContain("old-secret", error.Message);
    }

    [Fact]
    public void Rejects_noncanonical_key_names()
    {
        Assert.Throws<ArgumentException>(() => PvadEnvironment.Read("PATH", _ => null));
    }
}
