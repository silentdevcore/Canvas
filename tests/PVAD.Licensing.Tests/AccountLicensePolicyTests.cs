namespace PVAD.Licensing.Tests;

public sealed class AccountLicensePolicyTests
{
    [Theory]
    [InlineData("import.doc")]
    [InlineData("import.docx")]
    [InlineData("import.xlsx")]
    [InlineData("import.csv")]
    [InlineData("import.tsv")]
    [InlineData("import.odt")]
    [InlineData("import.pptx")]
    [InlineData("import.md")]
    [InlineData("import.html")]
    [InlineData("import.xml")]
    [InlineData("export.pdf")]
    [InlineData("export.docx")]
    [InlineData("export.xlsx")]
    [InlineData("export.odt")]
    [InlineData("document.create")]
    [InlineData("document.edit")]
    [InlineData("document.print")]
    [InlineData("template.create")]
    [InlineData("template.version")]
    [InlineData("template.publish")]
    [InlineData("code.edit")]
    [InlineData("code.execute")]
    [InlineData("api.access")]
    [InlineData("service-account.manage")]
    public void ConfirmedPrivateCapabilitiesAreEligible(string capability) =>
        Assert.Equal(PvadAccountLicenseDecision.Allowed,
            PvadAccountLicensePolicy.Evaluate(PvadLicenseAccountKind.IndividualDeveloper, capability));

    [Theory]
    [InlineData("import.pdf")]
    [InlineData("import.svg")]
    [InlineData("import.image")]
    [InlineData("image.analysis")]
    [InlineData("image.ocr")]
    [InlineData("export.pptx")]
    [InlineData("export.html")]
    [InlineData("export.xml")]
    [InlineData("export.md")]
    [InlineData("export.csv")]
    [InlineData("export.tsv")]
    [InlineData("export.svg")]
    [InlineData("export.png")]
    [InlineData("export.jpeg")]
    [InlineData("export.tiff")]
    [InlineData("pdf.signature.sign")]
    [InlineData("pdf.redaction.apply")]
    [InlineData("pdf.compliance.convert")]
    [InlineData("pdf.e-invoice.create")]
    [InlineData("pdf.e-invoice.validate")]
    public void PrivateRestrictionsDoNotRestrictCompanyEligibility(string capability)
    {
        Assert.Equal(PvadAccountLicenseDecision.Denied,
            PvadAccountLicensePolicy.Evaluate(PvadLicenseAccountKind.IndividualDeveloper, capability));
        Assert.Equal(PvadAccountLicenseDecision.Allowed,
            PvadAccountLicensePolicy.Evaluate(PvadLicenseAccountKind.Company, capability));
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("tsv")]
    [InlineData("md")]
    [InlineData("html")]
    [InlineData("xml")]
    [InlineData("pptx")]
    public void ImportPermissionDoesNotAuthorizeExportOfTheSameFormat(string format)
    {
        Assert.Equal(PvadAccountLicenseDecision.Allowed,
            PvadAccountLicensePolicy.Evaluate(PvadLicenseAccountKind.IndividualDeveloper, "import." + format));
        Assert.Equal(PvadAccountLicenseDecision.Denied,
            PvadAccountLicensePolicy.Evaluate(PvadLicenseAccountKind.IndividualDeveloper, "export." + format));
    }

    [Fact]
    public void EveryMigrationProductRequiresSeparateApprovalForBothAccountTypes()
    {
        var products = PvadLicenseProducts.All.Where(PvadLicenseProducts.IsMigration).ToArray();
        Assert.NotEmpty(products);
        foreach (var product in products)
        foreach (var account in Enum.GetValues<PvadLicenseAccountKind>())
            Assert.Equal(PvadAccountLicenseDecision.RequiresAdminApproval,
                PvadAccountLicensePolicy.Evaluate(account, product));
        Assert.Equal(products.Order(), PvadAccountLicensePolicy.Capabilities
            .Where(capability => capability.Id.StartsWith("migration.", StringComparison.Ordinal))
            .Select(capability => capability.Id).Order());
    }

    [Theory]
    [InlineData("import.xls")]
    [InlineData("export.xls")]
    public void DeferredXlsDecisionIsNotTreatedAsAFreeLaunchApproval(string capability)
    {
        foreach (var account in Enum.GetValues<PvadLicenseAccountKind>())
            Assert.Equal(PvadAccountLicenseDecision.Deferred, PvadAccountLicensePolicy.Evaluate(account, capability));
    }

    [Theory]
    [InlineData("pdf.trust.inspect")]
    [InlineData("pdf.signature.validate")]
    [InlineData("pdf.redaction.inspect")]
    [InlineData("pdf.compliance.validate")]
    [InlineData("pdf.e-invoice.read")]
    [InlineData("pdf.e-invoice.attach")]
    public void UndecidedInspectionAndReadRightsAreNotInvented(string capability) =>
        Assert.Equal(PvadAccountLicenseDecision.Undecided,
            PvadAccountLicensePolicy.Evaluate(PvadLicenseAccountKind.IndividualDeveloper, capability));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("import.*")]
    [InlineData("converter")]
    [InlineData("importer")]
    [InlineData("IMPORT.DOCX")]
    [InlineData("import.docx ")]
    [InlineData("future.capability")]
    public void UnknownOrBroadProductNamesNeverAuthorizeACapability(string? capability)
    {
        foreach (var account in Enum.GetValues<PvadLicenseAccountKind>())
            Assert.Equal(PvadAccountLicenseDecision.Denied, PvadAccountLicensePolicy.Evaluate(account, capability));
    }

    [Fact]
    public void InvalidAccountKindIsDeniedEvenForOtherwiseAllowedCapabilities() =>
        Assert.All(PvadAccountLicensePolicy.Capabilities, capability =>
            Assert.Equal(PvadAccountLicenseDecision.Denied,
                PvadAccountLicensePolicy.Evaluate((PvadLicenseAccountKind)99, capability.Id)));

    [Theory]
    [InlineData("png")]
    [InlineData(".JPG")]
    [InlineData("jpeg")]
    [InlineData("gif")]
    [InlineData("webp")]
    [InlineData("bmp")]
    [InlineData("tif")]
    [InlineData(".TIFF")]
    [InlineData("svg")]
    [InlineData(".PDF")]
    public void ImportExtensionsCannotBypassPrivateImageSvgAndPdfRestrictions(string format)
    {
        Assert.Equal(PvadAccountLicenseDecision.Denied,
            PvadAccountLicensePolicy.EvaluateImport(PvadLicenseAccountKind.IndividualDeveloper, format));
        Assert.Equal(PvadAccountLicenseDecision.Allowed,
            PvadAccountLicensePolicy.EvaluateImport(PvadLicenseAccountKind.Company, format));
    }

    [Theory]
    [InlineData(".MARKDOWN")]
    [InlineData(" .CSV ")]
    [InlineData(".TSV")]
    [InlineData("html")]
    [InlineData("htm")]
    [InlineData(".XML")]
    public void FormatAliasesKeepIndependentImportAndExportDecisions(string format)
    {
        Assert.Equal(PvadAccountLicenseDecision.Allowed,
            PvadAccountLicensePolicy.EvaluateImport(PvadLicenseAccountKind.IndividualDeveloper, format));
        Assert.Equal(PvadAccountLicenseDecision.Denied,
            PvadAccountLicensePolicy.EvaluateExport(PvadLicenseAccountKind.IndividualDeveloper, format));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("unsupported")]
    [InlineData("file.docx")]
    public void UnrecognizedFormatIdentifiersFailClosed(string? format)
    {
        foreach (var account in Enum.GetValues<PvadLicenseAccountKind>())
        {
            Assert.Equal(PvadAccountLicenseDecision.Denied, PvadAccountLicensePolicy.EvaluateImport(account, format));
            Assert.Equal(PvadAccountLicenseDecision.Denied, PvadAccountLicensePolicy.EvaluateExport(account, format));
        }
    }

    [Theory]
    [InlineData(".JPG")]
    [InlineData(".TIF")]
    [InlineData("png")]
    [InlineData("svg")]
    public void ExportAliasesDoNotEvadePrivateOutputRestrictions(string format)
    {
        Assert.Equal(PvadAccountLicenseDecision.Denied,
            PvadAccountLicensePolicy.EvaluateExport(PvadLicenseAccountKind.IndividualDeveloper, format));
        Assert.Equal(PvadAccountLicenseDecision.Allowed,
            PvadAccountLicensePolicy.EvaluateExport(PvadLicenseAccountKind.Company, format));
    }

    [Fact]
    public void CapabilityCatalogCannotBeModifiedByACaller()
    {
        var capabilities = Assert.IsAssignableFrom<IList<PvadAccountCapability>>(PvadAccountLicensePolicy.Capabilities);
        Assert.True(capabilities.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => capabilities.Clear());
    }
}
