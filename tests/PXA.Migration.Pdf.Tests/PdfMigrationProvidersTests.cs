namespace PXA.Migration.Pdf.Tests;

using PXA.Migration.Abstractions;

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
        { PdfMigrationProviderKeys.Nutrient, "PXA.Migration.Pdf.Code.Nutrient.NutrientMigration" },
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

    [Fact]
    public void TrustCatalog_CoversEveryProviderAndCapability()
    {
        Assert.Equal(PdfMigrationProviders.Keys, PdfTrustMigrationCatalog.All.Select(static item => item.ProviderKey));

        foreach (var profile in PdfTrustMigrationCatalog.All)
        {
            Assert.Equal(Enum.GetValues<PdfTrustCapability>(), profile.Capabilities.Select(static item => item.Capability));
            Assert.All(profile.Capabilities, capability =>
            {
                Assert.Equal(PdfTrustMigrationDisposition.ReviewRequired, capability.DefaultDisposition);
                Assert.StartsWith("/docs/", capability.DocumentationPath, StringComparison.Ordinal);
            });
            Assert.Contains("PDF/A-4", profile.UnsupportedTargetProfiles);
            Assert.Contains("PDF/UA-2", profile.UnsupportedTargetProfiles);
        }
    }

    [Fact]
    public void TrustPolicy_RequiresEquivalentCompleteAndValidatedSemantics()
    {
        Assert.Equal(
            PdfTrustMigrationDisposition.Automatic,
            PdfTrustMigrationPolicy.ResolveDisposition(
                PdfTrustSourceSemantics.PasswordEncryption,
                targetHasEquivalentSemantics: true,
                providerOptionsAreComplete: true,
                targetCanValidateEndToEnd: true));
        Assert.Equal(
            PdfTrustMigrationDisposition.ReviewRequired,
            PdfTrustMigrationPolicy.ResolveDisposition(
                PdfTrustSourceSemantics.PasswordEncryption,
                targetHasEquivalentSemantics: true,
                providerOptionsAreComplete: false,
                targetCanValidateEndToEnd: true));
    }

    [Theory]
    [InlineData(PdfTrustSourceSemantics.SignatureAppearanceOnly)]
    [InlineData(PdfTrustSourceSemantics.VisualRedactionOnly)]
    public void TrustPolicy_NeverPromotesVisualMarksToTrustClaims(PdfTrustSourceSemantics semantics)
    {
        Assert.Equal(
            PdfTrustMigrationDisposition.Unsupported,
            PdfTrustMigrationPolicy.ResolveDisposition(
                semantics,
                targetHasEquivalentSemantics: true,
                providerOptionsAreComplete: true,
                targetCanValidateEndToEnd: true));
    }
}
