namespace PVAD.Migration.Spreadsheet.Tests;

public sealed class SpreadsheetMigrationProvidersTests
{
    public static TheoryData<string, string> ProviderCases => new()
    {
        { SpreadsheetMigrationProviderKeys.AsposeCells, "PVAD.Migration.Spreadsheet.AsposeCellsMigration" },
        { SpreadsheetMigrationProviderKeys.ClosedXml, "PVAD.Migration.Spreadsheet.ClosedXmlSpreadsheetMigration" },
        { SpreadsheetMigrationProviderKeys.Epplus, "PVAD.Migration.Spreadsheet.EpplusSpreadsheetMigration" },
        { SpreadsheetMigrationProviderKeys.GemBoxSpreadsheet, "PVAD.Migration.Spreadsheet.GemBoxSpreadsheetMigration" },
        { SpreadsheetMigrationProviderKeys.Npoi, "PVAD.Migration.Spreadsheet.NpoiMigration" },
        { SpreadsheetMigrationProviderKeys.SpireXls, "PVAD.Migration.Spreadsheet.SpireXlsMigration" },
        { SpreadsheetMigrationProviderKeys.SpreadsheetLight, "PVAD.Migration.Spreadsheet.SpreadsheetLightMigration" },
        { SpreadsheetMigrationProviderKeys.SyncfusionXlsIo, "PVAD.Migration.Spreadsheet.SyncfusionXlsIoMigration" },
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
        Assert.Equal("PVAD.Migration.Spreadsheet.ClosedXmlSpreadsheetMigration", migration.GetType().FullName);
    }

    [Fact]
    public void Create_RejectsUnknownProviderKey()
    {
        Assert.Throws<KeyNotFoundException>(() => SpreadsheetMigrationProviders.Create("unknown-spreadsheet"));
    }
}
