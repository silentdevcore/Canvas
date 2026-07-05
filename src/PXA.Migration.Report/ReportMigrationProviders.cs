namespace PXA.Migration.Report;

public static class ReportMigrationProviders
{
    private static readonly IReadOnlyDictionary<string, Func<IReportMigration>> Factories =
        new Dictionary<string, Func<IReportMigration>>(StringComparer.OrdinalIgnoreCase)
        {
            [ReportMigrationProviderKeys.ActiveReportsJs] = static () => new ActiveReportsJsMigration(),
            [ReportMigrationProviderKeys.DevExpressReport] = static () => new DevExpressReportMigration(),
            [ReportMigrationProviderKeys.JasperReports] = static () => new JasperReportsMigration(),
            [ReportMigrationProviderKeys.Rdl] = static () => new RdlReportMigration(),
            [ReportMigrationProviderKeys.Rpx] = static () => new RpxReportMigration(),
        };

    public static IReadOnlyList<string> Keys { get; } = Factories.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();

    public static IReportMigration Create(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (TryCreate(key, out var migration))
            return migration;

        throw new KeyNotFoundException($"Unknown PXA report migration provider key '{key}'.");
    }

    public static bool TryCreate(string key, out IReportMigration migration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Factories.TryGetValue(key, out var factory))
        {
            migration = factory();
            return true;
        }

        migration = null!;
        return false;
    }
}
