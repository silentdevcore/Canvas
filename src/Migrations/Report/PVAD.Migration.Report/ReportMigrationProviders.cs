namespace PVAD.Migration.Report;

/// <summary>Lists and constructs built-in report Designer migration adapters.</summary>
/// <remarks>Lookup and construction are metadata operations. The selected adapter checks its
/// provider-specific native Report Designer Migration rights when conversion begins.</remarks>
public static class ReportMigrationProviders
{
    private static readonly IReadOnlyDictionary<string, Func<IReportMigration>> Factories =
        new Dictionary<string, Func<IReportMigration>>(StringComparer.OrdinalIgnoreCase)
        {
            [ReportMigrationProviderKeys.ActiveReportsJs] = static () => new ActiveReportsJsMigration(),
            [ReportMigrationProviderKeys.DevExpressReport] = static () => new DevExpressReportMigration(),
            [ReportMigrationProviderKeys.FastReport] = static () => new FastReportMigration(),
            [ReportMigrationProviderKeys.JasperReports] = static () => new JasperReportsMigration(),
            [ReportMigrationProviderKeys.Rdl] = static () => new RdlReportMigration(),
            [ReportMigrationProviderKeys.Rpx] = static () => new RpxReportMigration(),
            [ReportMigrationProviderKeys.Stimulsoft] = static () => new StimulsoftReportMigration(),
            [ReportMigrationProviderKeys.Telerik] = static () => new TelerikReportMigration(),
        };

    /// <summary>Gets the supported provider keys in ordinal sort order.</summary>
    public static IReadOnlyList<string> Keys { get; } = Factories.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();

    /// <summary>Constructs the selected adapter without converting a report.</summary>
    /// <param name="key">A nonblank provider key, matched without case sensitivity.</param>
    /// <returns>The built-in adapter.</returns>
    /// <exception cref="ArgumentException">The key is blank.</exception>
    /// <exception cref="KeyNotFoundException">The provider is not registered.</exception>
    public static IReportMigration Create(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (TryCreate(key, out var migration))
            return migration;

        throw new KeyNotFoundException($"Unknown PVAD report migration provider key '{key}'.");
    }

    /// <summary>Attempts to construct an adapter without starting a licensed conversion.</summary>
    /// <param name="key">A nonblank provider key, matched without case sensitivity.</param>
    /// <param name="migration">The adapter on success; null when the key is unknown.</param>
    /// <returns>True when a registered provider was found.</returns>
    /// <exception cref="ArgumentException">The key is blank.</exception>
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

    internal static ReportMigrationResult ConvertAdmitted(string provider, string source, IReadOnlyDictionary<string, string>? resources)
    {
        PVAD.Licensing.PvadLicense.DemandMigrationContinuation(PVAD.Licensing.PvadLicenseProducts.ReportDesignerMigration, provider);
        return provider switch
        {
            ReportMigrationProviderKeys.ActiveReportsJs => new ActiveReportsJsMigration().ConvertAdmitted(source),
            ReportMigrationProviderKeys.DevExpressReport => new DevExpressReportMigration().ConvertAdmitted(source, resources),
            ReportMigrationProviderKeys.FastReport => new FastReportMigration().ConvertAdmitted(source),
            ReportMigrationProviderKeys.JasperReports => new JasperReportsMigration().ConvertAdmitted(source, resources),
            ReportMigrationProviderKeys.Rdl => new RdlReportMigration().ConvertAdmitted(source),
            ReportMigrationProviderKeys.Rpx => new RpxReportMigration().ConvertAdmitted(source, resources),
            ReportMigrationProviderKeys.Stimulsoft => new StimulsoftReportMigration().ConvertAdmitted(source),
            ReportMigrationProviderKeys.Telerik => new TelerikReportMigration().ConvertAdmitted(source),
            _ => throw new ArgumentException($"Unknown report migration provider '{provider}'.", nameof(provider)),
        };
    }
}
