namespace PXA.Migration.Pdf.Tests;

public sealed class PdfMigrationProvidersTests
{
    public static TheoryData<string, string> ProviderCases => new()
    {
        { PdfMigrationProviderKeys.ActivePdf, "PXA.Migration.Pdf.Code.ActivePdf.ActivePdfMigration" },
        { PdfMigrationProviderKeys.Apryse, "PXA.Migration.Pdf.Code.Apryse.ApryseMigration" },
        { PdfMigrationProviderKeys.AsposePdf, "PXA.Migration.Pdf.Code.Aspose.AsposePdfMigration" },
        { PdfMigrationProviderKeys.DevExpressPdf, "PXA.Migration.Pdf.Code.DevExpress.DevExpressPdfMigration" },
        { PdfMigrationProviderKeys.DsPdf, "PXA.Migration.Pdf.Code.DsPdf.DsPdfMigration" },
        { PdfMigrationProviderKeys.FoxitPdf, "PXA.Migration.Pdf.Code.Foxit.FoxitPdfMigration" },
        { PdfMigrationProviderKeys.GemBoxPdf, "PXA.Migration.Pdf.Code.GemBox.GemBoxPdfMigration" },
        { PdfMigrationProviderKeys.IronPdf, "PXA.Migration.Pdf.Code.IronPdf.IronPdfMigration" },
        { PdfMigrationProviderKeys.IText7, "PXA.Migration.Pdf.Code.IText7.IText7Migration" },
        { PdfMigrationProviderKeys.LeadtoolsPdf, "PXA.Migration.Pdf.Code.Leadtools.LeadtoolsPdfMigration" },
        { PdfMigrationProviderKeys.PdfKitNet, "PXA.Migration.Pdf.Code.PdfKitNet.PdfKitNetMigration" },
        { PdfMigrationProviderKeys.PdfTools, "PXA.Migration.Pdf.Code.PdfTools.PdfToolsMigration" },
        { PdfMigrationProviderKeys.PdfToolsToolbox, "PXA.Migration.Pdf.Code.PdfToolsToolbox.PdfToolsToolboxMigration" },
        { PdfMigrationProviderKeys.SpirePdf, "PXA.Migration.Pdf.Code.Spire.SpirePdfMigration" },
        { PdfMigrationProviderKeys.SyncfusionPdf, "PXA.Migration.Pdf.Code.Syncfusion.SyncfusionPdfMigration" },
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
        Assert.Equal("PXA.Migration.Pdf.Code.DevExpress.DevExpressPdfMigration", migration.GetType().FullName);
    }

    [Fact]
    public void Create_RejectsUnknownProviderKey()
    {
        Assert.Throws<KeyNotFoundException>(() => PdfMigrationProviders.Create("unknown-pdf"));
    }
}
