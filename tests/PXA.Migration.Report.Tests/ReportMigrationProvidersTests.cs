namespace PXA.Migration.Report.Tests;

public sealed class ReportMigrationProvidersTests
{
    public static TheoryData<string, string> ProviderCases => new()
    {
        { ReportMigrationProviderKeys.DevExpressReport, "PXA.Migration.Report.DevExpressReportMigration" },
        { ReportMigrationProviderKeys.Rdl, "PXA.Migration.Report.RdlReportMigration" },
        { ReportMigrationProviderKeys.Rpx, "PXA.Migration.Report.RpxReportMigration" },
    };

    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void Create_ReturnsProviderForEveryKnownKey(string key, string expectedType)
    {
        var migration = ReportMigrationProviders.Create(key);

        Assert.Equal(expectedType, migration.GetType().FullName);
    }

    [Fact]
    public void Keys_ReturnsEveryKnownProviderKey()
    {
        var expected = ProviderCases.Select(static row => (string)row[0]).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, ReportMigrationProviders.Keys);
    }

    [Fact]
    public void TryCreate_IsCaseInsensitive()
    {
        var created = ReportMigrationProviders.TryCreate("DEVEXPRESS-REPORT", out var migration);

        Assert.True(created);
        Assert.Equal("PXA.Migration.Report.DevExpressReportMigration", migration.GetType().FullName);
    }

    [Fact]
    public void Create_RejectsUnknownProviderKey()
    {
        Assert.Throws<KeyNotFoundException>(() => ReportMigrationProviders.Create("unknown-report"));
    }
}
