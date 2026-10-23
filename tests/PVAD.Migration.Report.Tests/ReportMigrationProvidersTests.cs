namespace PVAD.Migration.Report.Tests;

public sealed class ReportMigrationProvidersTests
{
    public static TheoryData<string, string> ProviderCases => new()
    {
        { ReportMigrationProviderKeys.ActiveReportsJs, "PVAD.Migration.Report.ActiveReportsJsMigration" },
        { ReportMigrationProviderKeys.DevExpressReport, "PVAD.Migration.Report.DevExpressReportMigration" },
        { ReportMigrationProviderKeys.FastReport, "PVAD.Migration.Report.FastReportMigration" },
        { ReportMigrationProviderKeys.JasperReports, "PVAD.Migration.Report.JasperReportsMigration" },
        { ReportMigrationProviderKeys.Rdl, "PVAD.Migration.Report.RdlReportMigration" },
        { ReportMigrationProviderKeys.Rpx, "PVAD.Migration.Report.RpxReportMigration" },
        { ReportMigrationProviderKeys.Stimulsoft, "PVAD.Migration.Report.StimulsoftReportMigration" },
        { ReportMigrationProviderKeys.Telerik, "PVAD.Migration.Report.TelerikReportMigration" },
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
        Assert.Equal("PVAD.Migration.Report.DevExpressReportMigration", migration.GetType().FullName);
    }

    [Fact]
    public void Create_RejectsUnknownProviderKey()
    {
        Assert.Throws<KeyNotFoundException>(() => ReportMigrationProviders.Create("unknown-report"));
    }
}
