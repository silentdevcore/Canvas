namespace PVAD.Core.Tests;

public sealed class PvadTestEnvironmentTests
{
    [Theory]
    [InlineData("enabled", "enabled")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Only_canonical_test_settings_resolve(string? current, string? expected)
    {
        Assert.Equal(expected, PVAD.Testing.PvadTestEnvironment.Read("PVAD_TEST_FLAG",
            key => key == "PVAD_TEST_FLAG" ? current : "former-value"));
    }

    [Fact]
    public void Noncanonical_keys_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => PVAD.Testing.PvadTestEnvironment.Read("PATH", _ => null));
    }
}
