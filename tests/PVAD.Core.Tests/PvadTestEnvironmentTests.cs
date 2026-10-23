namespace PVAD.Core.Tests;

public sealed class PvadTestEnvironmentTests
{
    [Theory]
    [InlineData("enabled", null, "enabled")]
    [InlineData(null, "enabled", "enabled")]
    [InlineData("enabled", "enabled", "enabled")]
    [InlineData("", null, "")]
    [InlineData(null, null, null)]
    public void Canonical_and_legacy_test_settings_resolve(string? current, string? legacy, string? expected)
    {
        Assert.Equal(expected, PVAD.Testing.PvadTestEnvironment.Read("PVAD_TEST_FLAG",
            key => key == "PVAD_TEST_FLAG" ? current : legacy));
    }

    [Fact]
    public void Conflicting_test_settings_do_not_disclose_values()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            PVAD.Testing.PvadTestEnvironment.Read("PVAD_TEST_FLAG",
                key => key == "PVAD_TEST_FLAG" ? "new-private-value" : "old-private-value"));
        Assert.DoesNotContain("private-value", error.Message);
        Assert.Throws<ArgumentException>(() => PVAD.Testing.PvadTestEnvironment.Read("PATH", _ => null));
    }
}
