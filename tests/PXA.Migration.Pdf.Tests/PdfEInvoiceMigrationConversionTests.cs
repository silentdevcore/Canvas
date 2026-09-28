using PXA.Migration.Abstractions;
using PXA.Migration.Pdf;

namespace PXA.Migration.Pdf.Tests;

/// <summary>
/// Representative source-to-PXA conversions for every electronic-invoice provider family.
/// </summary>
/// <remarks>
/// <para>
/// The catalog tests prove the table is complete and honest. These prove it answers: given code a
/// customer actually has, the migration names the PXA equivalent, says whether a human has to
/// confirm it, and reports what PXA cannot carry across instead of dropping it.
/// </para>
/// <para>
/// The snippets are short, PXA-authored paraphrases of public SDK call shapes - no customer code and
/// no vendor source. Nothing here asserts that migrated output is conformant: conformance is
/// established by the pinned official artifact sets, which remain outstanding.
/// </para>
/// </remarks>
public sealed class PdfEInvoiceMigrationConversionTests
{
    [Fact]
    public void Every_capability_has_a_stable_diagnostic_identifier()
    {
        var codes = Enum.GetValues<PdfEInvoiceCapability>()
            .ToDictionary(static capability => capability, PdfEInvoiceMigrationCatalog.CodeFor);

        Assert.Equal("CANMIGEINV001", codes[PdfEInvoiceCapability.FacturX]);
        Assert.Equal("CANMIGEINV002", codes[PdfEInvoiceCapability.Zugferd]);
        Assert.Equal("CANMIGEINV003", codes[PdfEInvoiceCapability.XRechnung]);
        Assert.Equal("CANMIGEINV004", codes[PdfEInvoiceCapability.PdfA3Attachment]);
        Assert.Equal("CANMIGEINV005", codes[PdfEInvoiceCapability.InvoiceExtraction]);
        Assert.Equal("CANMIGEINV006", codes[PdfEInvoiceCapability.InvoiceValidation]);
        Assert.Equal(codes.Count, codes.Values.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// One snippet per provider that exercises every capability the provider has an API for, so no
    /// registered mapping goes unconverted.
    /// </summary>
    public static TheoryData<string> ProviderKeys()
    {
        var data = new TheoryData<string>();
        foreach (var profile in PdfEInvoiceMigrationCatalog.All) data.Add(profile.ProviderKey);
        return data;
    }

    [Theory]
    [MemberData(nameof(ProviderKeys))]
    public void Every_mapped_capability_converts_from_a_representative_snippet(string providerKey)
    {
        var profile = PdfEInvoiceMigrationCatalog.Get(providerKey);
        var mapped = profile.Capabilities
            .Where(static capability => capability.DetectedApiMarkers.Count > 0)
            .ToArray();

        foreach (var capability in mapped)
        {
            // A snippet that names every marker PXA recognises for this capability.
            var source = SnippetFor(profile.DisplayName, capability.DetectedApiMarkers);
            var findings = PdfEInvoiceMigrationCatalog.Analyze(providerKey, source);
            var finding = Assert.Single(findings, item => item.Capability == capability.Capability);

            Assert.Equal(providerKey, finding.ProviderKey);
            Assert.Equal(PdfEInvoiceMigrationCatalog.CodeFor(capability.Capability), finding.Code);
            Assert.Equal(capability.Disposition, finding.Disposition);
            Assert.Equal(capability.Rationale, finding.Rationale);
            Assert.Equal(capability.DocumentationPath, finding.DocumentationPath);
            Assert.Equal(capability.DetectedApiMarkers.OrderBy(static marker => marker, StringComparer.Ordinal),
                finding.MatchedMarkers.OrderBy(static marker => marker, StringComparer.Ordinal));
            // A mapped capability always names the PXA API that replaces it.
            Assert.False(string.IsNullOrWhiteSpace(finding.PxaEquivalent));
            Assert.NotEqual(PdfTrustMigrationDisposition.Unsupported, finding.Disposition);
        }
    }

    [Fact]
    public void Source_that_touches_nothing_converts_to_nothing()
    {
        var findings = PdfEInvoiceMigrationCatalog.Analyze(
            PdfMigrationProviderKeys.IText7,
            "var document = new Document(pdf);\ndocument.Add(new Paragraph(\"Hello\"));\ndocument.Close();");

        Assert.Empty(findings);
    }

    [Fact]
    public void A_marker_inside_a_longer_identifier_is_not_a_match()
    {
        // `Attachments` is an IronPDF marker; `CustomAttachmentsHelper` is not a use of it.
        var findings = PdfEInvoiceMigrationCatalog.Analyze(
            PdfMigrationProviderKeys.IronPdf,
            "var helper = new CustomAttachmentsHelper();\nhelper.AddAttachmentLater();");

        Assert.Empty(findings);
    }

    [Fact]
    public void Findings_and_markers_follow_source_order()
    {
        var source = """
            document.Validate(logPath, options);
            var second = FileSpecification.Contents;
            EmbeddedFileCollection first = document.EmbeddedFiles;
            """;

        var findings = PdfEInvoiceMigrationCatalog.Analyze(PdfMigrationProviderKeys.AsposePdf, source);

        Assert.Equal(
            [PdfEInvoiceCapability.InvoiceValidation, PdfEInvoiceCapability.PdfA3Attachment,
                PdfEInvoiceCapability.InvoiceExtraction],
            findings.Select(static finding => finding.Capability));
        Assert.Equal(["FileSpecification.Contents", "EmbeddedFileCollection"],
            findings[2].MatchedMarkers);
    }

    [Theory]
    [InlineData(PdfEInvoiceCapability.FacturX, "Factur-X", "CANMIGEINV001")]
    [InlineData(PdfEInvoiceCapability.Zugferd, "ZUGFeRD", "CANMIGEINV002")]
    [InlineData(PdfEInvoiceCapability.XRechnung, "XRechnung", "CANMIGEINV003")]
    public void A_standard_the_provider_has_no_api_for_is_reported_as_unsupported(
        PdfEInvoiceCapability capability,
        string standardName,
        string expectedCode)
    {
        // DevExpress PDF registers no generation API for any of the three standards, so a source
        // that clearly wants one must be told so rather than quietly losing it.
        var source = $"// Emit the {standardName} invoice for the customer\nWriteInvoice(order);";
        var findings = PdfEInvoiceMigrationCatalog.Analyze(PdfMigrationProviderKeys.DevExpressPdf, source);

        var finding = Assert.Single(findings, item => item.Capability == capability);
        Assert.Equal(PdfTrustMigrationDisposition.Unsupported, finding.Disposition);
        Assert.Equal(expectedCode, finding.Code);
        Assert.Null(finding.PxaEquivalent);
        Assert.Contains(standardName, finding.MatchedMarkers, StringComparer.OrdinalIgnoreCase);
        // Unsupported is an answer, not a gap: it still explains what to do instead.
        Assert.False(string.IsNullOrWhiteSpace(finding.Rationale));
    }

    [Fact]
    public void Invoice_generation_from_a_real_call_shape_always_needs_review()
    {
        // iText is the one registered provider with generation APIs PXA maps.
        var source = """
            var writer = new PdfWriter(stream);
            var pdf = new PdfADocument(writer, PdfAConformanceLevel.PDF_A_3B, outputIntent);
            var invoice = new PdfInvoice(pdf);
            invoice.SetProfile(InvoiceProfile.EN16931);
            invoice.SetFacturX(facturXXml);
            pdf.AddFileAttachment("factur-x.xml", facturXXml);
            """;

        var findings = PdfEInvoiceMigrationCatalog.Analyze(PdfMigrationProviderKeys.IText7, source);

        var generation = Assert.Single(findings, item => item.Capability == PdfEInvoiceCapability.FacturX);
        // Never automatic: the standard version and profile are a business obligation.
        Assert.Equal(PdfTrustMigrationDisposition.ReviewRequired, generation.Disposition);
        Assert.Contains("EInvoice", generation.PxaEquivalent!, StringComparison.Ordinal);
        Assert.DoesNotContain("conformant", generation.Rationale, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("compliant", generation.Rationale, StringComparison.OrdinalIgnoreCase);

        // The same snippet also attaches, which is a separate mapping with its own identifier.
        var attachment = Assert.Single(findings, item => item.Capability == PdfEInvoiceCapability.PdfA3Attachment);
        Assert.Equal("CANMIGEINV004", attachment.Code);
        Assert.NotEqual(generation.Code, attachment.Code);
    }

    [Fact]
    public void Attachment_extraction_and_validation_each_convert_on_their_own()
    {
        // Aspose.PDF registers attachment, extraction and validation, but no generation.
        var source = """
            EmbeddedFileCollection collection = document.EmbeddedFiles;
            var specification = new FileSpecification(path, "factur-x.xml");
            collection.Add(specification);
            var bytes = FileSpecification.Contents;
            document.Validate(logPath, PdfFormatConversionOptions.Default);
            """;

        var findings = PdfEInvoiceMigrationCatalog.Analyze(PdfMigrationProviderKeys.AsposePdf, source)
            .ToDictionary(static finding => finding.Capability);

        Assert.Equal("CANMIGEINV004", findings[PdfEInvoiceCapability.PdfA3Attachment].Code);
        Assert.Equal("CANMIGEINV005", findings[PdfEInvoiceCapability.InvoiceExtraction].Code);
        Assert.Equal("CANMIGEINV006", findings[PdfEInvoiceCapability.InvoiceValidation].Code);

        foreach (var capability in new[]
        {
            PdfEInvoiceCapability.PdfA3Attachment,
            PdfEInvoiceCapability.InvoiceExtraction,
            PdfEInvoiceCapability.InvoiceValidation
        })
        {
            Assert.NotEqual(PdfTrustMigrationDisposition.Unsupported, findings[capability].Disposition);
            Assert.False(string.IsNullOrWhiteSpace(findings[capability].PxaEquivalent));
        }

        // The reserved `factur-x.xml` filename says the source is producing a Factur-X invoice.
        // Aspose registers no generation API PXA can map, so that intent is reported as unsupported
        // rather than passed over because only the attachment call was recognised.
        var facturX = findings[PdfEInvoiceCapability.FacturX];
        Assert.Equal(PdfTrustMigrationDisposition.Unsupported, facturX.Disposition);
        Assert.Equal("CANMIGEINV001", facturX.Code);
        Assert.Null(facturX.PxaEquivalent);
    }

    [Fact]
    public void Every_registered_provider_key_survives_analysis()
    {
        // Provider identifiers are public API for the migration workbench; a rename would break
        // saved reports, so the analyzer must answer to exactly the registered keys.
        foreach (var profile in PdfEInvoiceMigrationCatalog.All)
        {
            var findings = PdfEInvoiceMigrationCatalog.Analyze(profile.ProviderKey, "// nothing here");
            Assert.Empty(findings);
        }

        Assert.Throws<KeyNotFoundException>(() =>
            PdfEInvoiceMigrationCatalog.Analyze("not-a-provider", "source"));
    }

    [Fact]
    public void Migration_output_never_claims_official_conformance()
    {
        foreach (var profile in PdfEInvoiceMigrationCatalog.All)
        {
            var source = SnippetFor(
                profile.DisplayName,
                [.. profile.Capabilities.SelectMany(static capability => capability.DetectedApiMarkers)]);
            foreach (var finding in PdfEInvoiceMigrationCatalog.Analyze(profile.ProviderKey, source))
            {
                Assert.DoesNotContain("conformant", finding.Rationale, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("certified", finding.Rationale, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("validated", finding.Rationale, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static string SnippetFor(string displayName, IReadOnlyList<string> markers) =>
        $"// Migrated from {displayName}\n"
        + string.Join('\n', markers.Select(static marker => $"Use({marker});"));
}
