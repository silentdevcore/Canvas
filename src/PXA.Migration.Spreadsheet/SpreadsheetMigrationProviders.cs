using PXA.Migration.Abstractions;

namespace PXA.Migration.Spreadsheet;

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

    public static IReadOnlyList<string> Keys { get; } = Factories.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();

    public static ISourceMigration Create(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (TryCreate(key, out var migration))
            return migration;

        throw new KeyNotFoundException($"Unknown PXA spreadsheet migration provider key '{key}'.");
    }

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
}
