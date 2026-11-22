using PVAD.Migration.Abstractions;
using PVAD.Migration.Roslyn;
using PVAD.Licensing;

namespace PVAD.Migration.Spreadsheet;

/// <summary>Lists, constructs and invokes the built-in Spreadsheet Code Migration providers.</summary>
/// <remarks>Provider discovery is license-free metadata. Migration requires native Spreadsheet
/// Code Migration rights for the chosen provider, not PDF Code Migration rights.</remarks>
public static class SpreadsheetMigrationProviders
{
    private static readonly IReadOnlyDictionary<string, Func<ISourceMigration>> Factories =
        new Dictionary<string, Func<ISourceMigration>>(StringComparer.OrdinalIgnoreCase)
        {
            [SpreadsheetMigrationProviderKeys.AsposeCells] = static () => new AsposeCellsMigration(),
            [SpreadsheetMigrationProviderKeys.ClosedXml] = static () => new ClosedXmlSpreadsheetMigration(),
            [SpreadsheetMigrationProviderKeys.Epplus] = static () => new EpplusSpreadsheetMigration(),
            [SpreadsheetMigrationProviderKeys.GemBoxSpreadsheet] = static () => new GemBoxSpreadsheetMigration(),
            [SpreadsheetMigrationProviderKeys.Npoi] = static () => new NpoiMigration(),
            [SpreadsheetMigrationProviderKeys.SpireXls] = static () => new SpireXlsMigration(),
            [SpreadsheetMigrationProviderKeys.SpreadsheetLight] = static () => new SpreadsheetLightMigration(),
            [SpreadsheetMigrationProviderKeys.SyncfusionXlsIo] = static () => new SyncfusionXlsIoMigration(),
        };

    /// <summary>Gets the supported provider keys in ordinal sort order.</summary>
    public static IReadOnlyList<string> Keys { get; } = Factories.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();

    /// <summary>Constructs an adapter without migrating source or loading a native license.</summary>
    /// <param name="key">A nonblank provider key, matched without case sensitivity.</param>
    /// <returns>The built-in source converter.</returns>
    /// <exception cref="ArgumentException">The key is blank.</exception>
    /// <exception cref="KeyNotFoundException">The provider is not registered.</exception>
    public static ISourceMigration Create(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (TryCreate(key, out var migration))
            return migration;

        throw new KeyNotFoundException($"Unknown PVAD spreadsheet migration provider key '{key}'.");
    }

    /// <summary>Attempts provider lookup without starting a licensed migration.</summary>
    /// <param name="key">A nonblank provider key, matched without case sensitivity.</param>
    /// <param name="migration">The adapter on success; null for an unknown key.</param>
    /// <returns>True when a provider was found.</returns>
    /// <exception cref="ArgumentException">The key is blank.</exception>
    public static bool TryCreate(string key, out ISourceMigration migration)
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

    /// <summary>Admits the selected native provider and converts source without executing it.</summary>
    /// <param name="key">A registered Spreadsheet provider key.</param>
    /// <param name="sourceCode">Source text accepted by that provider.</param>
    /// <returns>Generated Spreadsheet code and diagnostics requiring review.</returns>
    /// <exception cref="PvadLicenseException">Native Spreadsheet Code Migration rights for this provider are unavailable.</exception>
    public static MigrationResult Migrate(string key, string sourceCode)
    {
        var migration = Create(key);
        var provider = MigrationProviderCatalog.Get(MigrationDomains.Spreadsheet, MigrationKinds.Code, key).ProviderKey;
        using var operation = CodeMigrationAdmission.Begin(PvadLicenseProducts.SpreadsheetCodeMigration, provider);
        return CodeMigrationAdmission.MigrateAdmitted(migration, PvadLicenseProducts.SpreadsheetCodeMigration, provider, sourceCode);
    }

    internal static MigrationResult MigrateAdmitted(string key, string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(Create(key), PvadLicenseProducts.SpreadsheetCodeMigration, key, sourceCode);
}
