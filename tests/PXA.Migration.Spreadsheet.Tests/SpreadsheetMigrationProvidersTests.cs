namespace PXA.Migration.Spreadsheet.Tests;

public sealed class SpreadsheetMigrationProvidersTests
{
    public static TheoryData<string, string> ProviderCases => new()
    {
        { SpreadsheetMigrationProviderKeys.AsposeCells, "PXA.Migration.Spreadsheet.AsposeCellsMigration" },
        { SpreadsheetMigrationProviderKeys.ClosedXml, "PXA.Migration.Spreadsheet.ClosedXmlSpreadsheetMigration" },
        { SpreadsheetMigrationProviderKeys.Epplus, "PXA.Migration.Spreadsheet.EpplusSpreadsheetMigration" },
        { SpreadsheetMigrationProviderKeys.GemBoxSpreadsheet, "PXA.Migration.Spreadsheet.GemBoxSpreadsheetMigration" },
        { SpreadsheetMigrationProviderKeys.Npoi, "PXA.Migration.Spreadsheet.NpoiMigration" },
        { SpreadsheetMigrationProviderKeys.SpireXls, "PXA.Migration.Spreadsheet.SpireXlsMigration" },
        { SpreadsheetMigrationProviderKeys.SpreadsheetLight, "PXA.Migration.Spreadsheet.SpreadsheetLightMigration" },
        { SpreadsheetMigrationProviderKeys.SyncfusionXlsIo, "PXA.Migration.Spreadsheet.SyncfusionXlsIoMigration" },
    };

    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void Create_ReturnsProviderForEveryKnownKey(string key, string expectedType)
    {
        var migration = SpreadsheetMigrationProviders.Create(key);

        Assert.Equal(expectedType, migration.GetType().FullName);
    }

    [Fact]
    public void Keys_ReturnsEveryKnownProviderKey()
    {
        var expected = ProviderCases.Select(static row => (string)row[0]).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, SpreadsheetMigrationProviders.Keys);
    }

    [Fact]
    public void TryCreate_IsCaseInsensitive()
    {
        var created = SpreadsheetMigrationProviders.TryCreate("CLOSEDXML", out var migration);

        Assert.True(created);
        Assert.Equal("PXA.Migration.Spreadsheet.ClosedXmlSpreadsheetMigration", migration.GetType().FullName);
    }

    [Fact]
    public void Create_RejectsUnknownProviderKey()
    {
        Assert.Throws<KeyNotFoundException>(() => SpreadsheetMigrationProviders.Create("unknown-spreadsheet"));
    }
}
