using PVAD.WebApi.Services.Licensing;

namespace PVAD.Api.Tests;

public sealed class PvadLicenseTimestampsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 10)]
    [InlineData(19, 10)]
    public void Signed_timestamps_use_idempotent_database_precision(int extraTicks, int expectedTicks)
    {
        var start = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.FromHours(2));

        var normalized = PvadLicenseTimestamps.Normalize(start.AddTicks(extraTicks));

        Assert.Equal(start.ToUniversalTime().AddTicks(expectedTicks), normalized);
        Assert.Equal(TimeSpan.Zero, normalized.Offset);
        Assert.Equal(normalized, PvadLicenseTimestamps.Normalize(normalized));
    }
}
