namespace PXA.Migration.Pdf.Tests;

public sealed class PdfMigrationProvidersTests
{
    public static TheoryData<string, string> ProviderCases => new()
    {
        { PdfMigrationProviderKeys.ActivePdf, "PXA.Migration.ActivePdf.ActivePdfMigration" },
        { PdfMigrationProviderKeys.Apryse, "PXA.Migration.Apryse.ApryseMigration" },
        { PdfMigrationProviderKeys.AsposePdf, "PXA.Migration.AsposePdf.AsposePdfMigration" },
        { PdfMigrationProviderKeys.DevExpressPdf, "PXA.Migration.DevExpressPdf.DevExpressPdfMigration" },
        { PdfMigrationProviderKeys.DsPdf, "PXA.Migration.DsPdf.DsPdfMigration" },
        { PdfMigrationProviderKeys.FoxitPdf, "PXA.Migration.FoxitPdf.FoxitPdfMigration" },
        { PdfMigrationProviderKeys.GemBoxPdf, "PXA.Migration.GemBoxPdf.GemBoxPdfMigration" },
        { PdfMigrationProviderKeys.IronPdf, "PXA.Migration.IronPdf.IronPdfMigration" },
        { PdfMigrationProviderKeys.IText7, "PXA.Migration.iText7.IText7Migration" },
        { PdfMigrationProviderKeys.LeadtoolsPdf, "PXA.Migration.LeadtoolsPdf.LeadtoolsPdfMigration" },
        { PdfMigrationProviderKeys.PdfKitNet, "PXA.Migration.PdfKitNet.PdfKitNetMigration" },
        { PdfMigrationProviderKeys.PdfTools, "PXA.Migration.PdfTools.PdfToolsMigration" },
        { PdfMigrationProviderKeys.PdfToolsToolbox, "PXA.Migration.PdfToolsToolbox.PdfToolsToolboxMigration" },
        { PdfMigrationProviderKeys.SpirePdf, "PXA.Migration.SpirePdf.SpirePdfMigration" },
        { PdfMigrationProviderKeys.SyncfusionPdf, "PXA.Migration.SyncfusionPdf.SyncfusionPdfMigration" },
    };

    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void Create_ReturnsProviderForEveryKnownKey(string key, string expectedType)
    {
        var migration = PdfMigrationProviders.Create(key);

        Assert.Equal(expectedType, migration.GetType().FullName);
    }

    [Fact]
    public void Keys_ReturnsEveryKnownProviderKey()
    {
        var expected = ProviderCases.Select(static row => (string)row[0]).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, PdfMigrationProviders.Keys);
    }

    [Fact]
    public void TryCreate_IsCaseInsensitive()
    {
        var created = PdfMigrationProviders.TryCreate("DEVEXPRESS-PDF", out var migration);

        Assert.True(created);
        Assert.Equal("PXA.Migration.DevExpressPdf.DevExpressPdfMigration", migration.GetType().FullName);
    }

    [Fact]
    public void Create_RejectsUnknownProviderKey()
    {
        Assert.Throws<KeyNotFoundException>(() => PdfMigrationProviders.Create("unknown-pdf"));
    }
}
