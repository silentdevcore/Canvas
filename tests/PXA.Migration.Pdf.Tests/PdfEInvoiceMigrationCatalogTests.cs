using PXA.Migration.Abstractions;
using PXA.Migration.Pdf;

namespace PXA.Migration.Pdf.Tests;

/// <summary>
/// EINV-06: every PDF code-migration provider answers for every electronic-invoice capability,
/// with one of three dispositions and a stated reason. No capability may be left implicit.
/// </summary>
public sealed class PdfEInvoiceMigrationCatalogTests
{
    public static TheoryData<string> ProviderKeys()
    {
        var data = new TheoryData<string>();
        foreach (var profile in PdfEInvoiceMigrationCatalog.All)
        {
            data.Add(profile.ProviderKey);
        }

        return data;
    }

    [Fact]
    public void Every_pdf_code_migration_provider_is_covered()
    {
        var expected = PdfTrustMigrationCatalog.All
            .Select(profile => profile.ProviderKey)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            expected,
            PdfEInvoiceMigrationCatalog.All.Select(profile => profile.ProviderKey).ToArray());
    }

    [Theory]
    [MemberData(nameof(ProviderKeys))]
    public void Every_capability_has_a_disposition_and_a_reason(string providerKey)
    {
        var profile = PdfEInvoiceMigrationCatalog.Get(providerKey);

        Assert.Equal(
            Enum.GetValues<PdfEInvoiceCapability>().Order().ToArray(),
            profile.Capabilities.Select(capability => capability.Capability).Order().ToArray());
        foreach (var capability in profile.Capabilities)
        {
            Assert.True(
                Enum.IsDefined(capability.Disposition),
                $"{providerKey}/{capability.Capability} has no valid disposition.");
            Assert.False(
                string.IsNullOrWhiteSpace(capability.Rationale),
                $"{providerKey}/{capability.Capability} must state why.");
            Assert.False(
                string.IsNullOrWhiteSpace(capability.DocumentationPath),
                $"{providerKey}/{capability.Capability} must link to documentation.");
            // "Unsupported" is the only disposition allowed to have no PXA equivalent, and a
            // supported one is never left without markers to detect it.
            if (capability.Disposition == PdfTrustMigrationDisposition.Unsupported)
            {
                Assert.Empty(capability.DetectedApiMarkers);
                Assert.Null(capability.PxaEquivalent);
            }
            else
            {
                Assert.NotEmpty(capability.DetectedApiMarkers);
                Assert.False(string.IsNullOrWhiteSpace(capability.PxaEquivalent));
            }
        }
    }

    [Fact]
    public void No_rationale_is_a_silent_todo()
    {
        foreach (var capability in PdfEInvoiceMigrationCatalog.All.SelectMany(p => p.Capabilities))
        {
            Assert.DoesNotContain("TODO", capability.Rationale, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("not implemented", capability.Rationale, StringComparison.OrdinalIgnoreCase);
            Assert.True(capability.Rationale.Length > 40, "A rationale has to explain itself.");
        }
    }

    [Fact]
    public void Invoice_generation_always_requires_review_and_never_claims_conformance()
    {
        var generation = new[]
        {
            PdfEInvoiceCapability.FacturX,
            PdfEInvoiceCapability.Zugferd,
            PdfEInvoiceCapability.XRechnung
        };

        foreach (var capability in PdfEInvoiceMigrationCatalog.All
                     .SelectMany(profile => profile.Capabilities)
                     .Where(capability => generation.Contains(capability.Capability)))
        {
            Assert.NotEqual(PdfTrustMigrationDisposition.Automatic, capability.Disposition);
            Assert.DoesNotContain("conformant", capability.Rationale, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("compliant", capability.Rationale, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Validation_is_never_automatic_because_the_source_may_check_something_else()
    {
        foreach (var capability in PdfEInvoiceMigrationCatalog.All
                     .Select(profile => profile.Capability(PdfEInvoiceCapability.InvoiceValidation)))
        {
            Assert.NotEqual(PdfTrustMigrationDisposition.Automatic, capability.Disposition);
        }
    }

    [Fact]
    public void Embedding_and_extracting_a_file_map_automatically_where_the_provider_has_the_api()
    {
        var attachment = PdfEInvoiceMigrationCatalog
            .Get(PdfMigrationProviderKeys.AsposePdf)
            .Capability(PdfEInvoiceCapability.PdfA3Attachment);
        var extraction = PdfEInvoiceMigrationCatalog
            .Get(PdfMigrationProviderKeys.AsposePdf)
            .Capability(PdfEInvoiceCapability.InvoiceExtraction);

        Assert.Equal(PdfTrustMigrationDisposition.Automatic, attachment.Disposition);
        Assert.Contains("AddAssociatedFile", attachment.PxaEquivalent!, StringComparison.Ordinal);
        Assert.Equal(PdfTrustMigrationDisposition.Automatic, extraction.Disposition);
    }

    [Fact]
    public void A_provider_with_a_named_invoice_api_maps_it_rather_than_dropping_it()
    {
        var itext = PdfEInvoiceMigrationCatalog.Get(PdfMigrationProviderKeys.IText7);

        var facturX = itext.Capability(PdfEInvoiceCapability.FacturX);
        var zugferd = itext.Capability(PdfEInvoiceCapability.Zugferd);
        Assert.Equal(PdfTrustMigrationDisposition.ReviewRequired, facturX.Disposition);
        Assert.Equal(PdfTrustMigrationDisposition.ReviewRequired, zugferd.Disposition);
        Assert.Contains("ZugferdDocument", zugferd.DetectedApiMarkers);
        Assert.Contains("EInvoice.GenerateXml", facturX.PxaEquivalent!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_provider_without_an_invoice_api_says_so_instead_of_implying_support()
    {
        var leadtools = PdfEInvoiceMigrationCatalog.Get(PdfMigrationProviderKeys.LeadtoolsPdf);

        var xrechnung = leadtools.Capability(PdfEInvoiceCapability.XRechnung);
        Assert.Equal(PdfTrustMigrationDisposition.Unsupported, xrechnung.Disposition);
        Assert.Contains("does not invent one", xrechnung.Rationale, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_capability_links_to_the_invoicing_guide()
    {
        foreach (var capability in Enum.GetValues<PdfEInvoiceCapability>())
        {
            Assert.StartsWith(
                "/docs/guides/PXA.EInvoicing.html",
                PdfEInvoiceMigrationCatalog.DocumentationFor(capability),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Api_markers_are_unique_within_a_capability()
    {
        foreach (var profile in PdfEInvoiceMigrationCatalog.All)
        {
            foreach (var capability in profile.Capabilities)
            {
                Assert.Equal(
                    capability.DetectedApiMarkers.Distinct(StringComparer.Ordinal).Count(),
                    capability.DetectedApiMarkers.Count);
            }
        }
    }

    [Fact]
    public void Unknown_provider_keys_fail_loudly()
    {
        Assert.Throws<KeyNotFoundException>(() => PdfEInvoiceMigrationCatalog.Get("not-a-provider"));
        Assert.False(PdfEInvoiceMigrationCatalog.TryGet("not-a-provider", out _));
        Assert.True(PdfEInvoiceMigrationCatalog.TryGet(PdfMigrationProviderKeys.Nutrient, out var profile));
        Assert.Equal("Nutrient SDK", profile.DisplayName);
    }

    [Fact]
    public void Providers_with_invoice_capabilities_are_the_ones_with_a_mapped_disposition()
    {
        Assert.NotEmpty(PdfEInvoiceMigrationCatalog.WithInvoiceCapabilities);
        foreach (var profile in PdfEInvoiceMigrationCatalog.WithInvoiceCapabilities)
        {
            Assert.Contains(
                profile.Capabilities,
                capability => capability.Disposition != PdfTrustMigrationDisposition.Unsupported);
        }
    }
}
