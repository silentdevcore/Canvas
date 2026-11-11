using PVAD.Infrastructure.Persistence.Configuration;

namespace PVAD.Api.Tests;

public sealed class PvadEnvironmentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("configured")]
    [InlineData("")]
    public void Reads_only_the_canonical_value(string? current)
    {
        var result = PvadEnvironment.Read("PVAD_API_KEY", key => key switch
        {
            "PVAD_API_KEY" => current,
            "PXA_API_KEY" => "former-value",
            _ => throw new InvalidOperationException("Unexpected key"),
        });
        Assert.Equal(current, result);
    }

    [Fact]
    public void Rejects_noncanonical_key_names()
    {
        Assert.Throws<ArgumentException>(() => PvadEnvironment.Read("PATH", _ => null));
    }
}
