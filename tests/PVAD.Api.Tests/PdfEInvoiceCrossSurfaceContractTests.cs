using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PVAD.Generator;
using PVAD.Importer.EInvoicing;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Pdf;
using PVAD.WebApi.Controllers;

namespace PVAD.Api.Tests;

/// <summary>
/// One vocabulary and one set of figures across every surface that mentions an invoice.
/// </summary>
/// <remarks>
/// <para>
/// PXA states the same invoice facts in seven places: the Designer, the API, the Viewer's
/// inspection, the migration catalog, the generator, the importer's recognition and the public
/// documentation. If any two disagree about which version is maintained, which profile is offered,
/// which syntax a standard uses, or what a conformance state is called, a customer is told two
/// different things about one document.
/// </para>
/// <para>
/// The Designer and the documentation are checked by reading their sources, because that is where
/// those surfaces state the facts; a C# constant cannot stand in for them. Every figure comes from a
/// synthetic, repository-owned invoice.
/// </para>
/// </remarks>
public sealed partial class PdfEInvoiceCrossSurfaceContractTests
{
    // ── Standards, versions, profiles and syntax ─────────────────────────────

    [Theory]
    [InlineData("FacturX", "1.09.2")]
    [InlineData("ZUGFeRD", "2.5.2")]
    [InlineData("XRechnung", "3.0.2")]
    public void The_designer_and_the_generator_maintain_the_same_version(string standard, string version)
    {
        var generatorVersion = standard switch
        {
            "FacturX" => EInvoice.CurrentFacturXVersion,
            "ZUGFeRD" => EInvoice.CurrentZugferdVersion,
            _ => EInvoice.CurrentXRechnungVersion
        };
        Assert.Equal(version, generatorVersion);

        // The Designer states its own maintained versions; they have to be the same string.
        var designer = DesignerSource("features/e-invoice/invoiceModel.ts");
        var versions = Section(designer, "EINVOICE_VERSIONS");
        Assert.Contains($"{standard}: '{version}'", versions, StringComparison.Ordinal);
    }

    [Fact]
    public void The_designer_and_the_documentation_offer_the_same_profiles()
    {
        var profiles = Section(DesignerSource("features/e-invoice/invoiceModel.ts"), "EINVOICE_PROFILES");
        var guide = Documentation();

        foreach (var profile in new[] { "MINIMUM", "BASIC WL", "BASIC", "EN 16931", "EXTENDED" })
        {
            Assert.Contains($"'{profile}'", profiles, StringComparison.Ordinal);
            Assert.Contains(profile, guide, StringComparison.Ordinal);
        }

        // XRechnung has exactly one profile, on both surfaces.
        Assert.Contains("XRechnung: ['XRECHNUNG']", profiles, StringComparison.Ordinal);
        Assert.Contains("XRECHNUNG", guide, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PvadEInvoiceStandard.FacturX, "CII")]
    [InlineData(PvadEInvoiceStandard.Zugferd, "CII")]
    [InlineData(PvadEInvoiceStandard.XRechnung, "UBL-Invoice")]
    public void Generation_and_recognition_agree_on_the_syntax_of_each_standard(
        PvadEInvoiceStandard standard,
        string syntax)
    {
        var generated = EInvoice.GenerateXml(Invoice(), Options(standard));
        Assert.True(generated.IsGenerated);

        // The importer recognises what the generator produced, and names the same syntax.
        var recognized = EInvoice.Recognize(generated.Xml.ToArray());
        Assert.True(recognized.IsRecognized);
        Assert.Equal(syntax, recognized.Syntax);
    }

    [Theory]
    [InlineData(PvadEInvoiceStandard.FacturX, "factur-x.xml")]
    [InlineData(PvadEInvoiceStandard.Zugferd, "factur-x.xml")]
    [InlineData(PvadEInvoiceStandard.XRechnung, "xrechnung.xml")]
    public void Every_surface_uses_the_same_reserved_filename(
        PvadEInvoiceStandard standard,
        string fileName)
    {
        Assert.Equal(fileName, EInvoice.GetDefaultFileName(standard));
        // The documentation names the reserved filenames a recipient will look for.
        Assert.Contains(fileName, Documentation(), StringComparison.Ordinal);
    }

    // ── The three validation states ──────────────────────────────────────────

    [Fact]
    public void The_conformance_vocabulary_is_the_same_everywhere()
    {
        string[] states = ["valid", "invalid", "unavailable", "notEvaluated"];

        var designer = DesignerSource("features/e-invoice/invoiceModel.ts")
            + DesignerSource("features/e-invoice/invoiceValidation.ts");
        var guide = Documentation();

        foreach (var state in states)
        {
            Assert.Contains(state, designer, StringComparison.Ordinal);
            Assert.Contains(state, guide, StringComparison.Ordinal);
        }

        // "validated" was once used for "valid"; the Designer would then never have shown a real
        // verdict. Nothing may reintroduce it as a conformance status.
        Assert.DoesNotContain("conformanceStatus: 'validated'", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("\"conformanceStatus\": \"validated\"", guide, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Structural_recognition_is_never_presented_as_conformance_on_any_surface()
    {
        var generated = EInvoice.GenerateXml(Invoice(), Options(PvadEInvoiceStandard.FacturX));
        var result = await new PdfEInvoicesController().Validate(
            FormFile(generated.Xml.ToArray(), "factur-x.xml", "application/xml"),
            "Factur-X", EInvoice.CurrentFacturXVersion, "EN 16931", CancellationToken.None);
        var report = (PvadEInvoiceValidationReport)Assert.IsType<OkObjectResult>(result.Result).Value!;

        Assert.True(report.IsStructurallyValid);
        Assert.False(report.IsConformanceValidated);
        Assert.Equal("notEvaluated", report.ConformanceStatus);

        // The documentation says the same thing in words, and the migration catalog refuses to
        // claim conformance for anything it converts.
        Assert.Contains("not a conformance", Documentation(), StringComparison.OrdinalIgnoreCase);
        foreach (var profile in PdfEInvoiceMigrationCatalog.All)
        {
            foreach (var capability in profile.Capabilities)
            {
                Assert.DoesNotContain("conformant", capability.Rationale, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ── The figures themselves ───────────────────────────────────────────────

    [Fact]
    public async Task Generator_importer_api_and_viewer_report_one_set_of_figures()
    {
        var invoice = Invoice();
        var fonts = new PVAD.Pdf.PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"));

        var created = EInvoice.CreatePdf(invoice, Options(PvadEInvoiceStandard.FacturX), fonts);
        Assert.True(created.IsGenerated);

        // What the Viewer shows comes from inspecting the finished document, not from the request.
        var inspected = await new PdfEInvoicesController().Inspect(
            FormFile(created.Pdf.ToArray(), "invoice.pdf", "application/pdf"), CancellationToken.None);
        var summary = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PdfEInvoiceAttachmentSummary>>(
            Assert.IsType<OkObjectResult>(inspected.Result).Value));
        var semantic = summary.SemanticData;
        Assert.NotNull(semantic);

        // Identity and dates.
        Assert.Equal(invoice.InvoiceNumber, semantic!.InvoiceNumber);
        // Parties.
        Assert.Equal(invoice.Seller.Name, semantic.Seller.Name);
        Assert.Equal(invoice.Buyer.Name, semantic.Buyer.Name);
        // Currency.
        Assert.Equal(invoice.Currency, semantic.Currency);
        // Lines, tax groups and totals, as exact decimals.
        // 1 x 100.00 at 19% and 2 x 30.00 at 7%: net 160.00, tax 19.00 + 4.20, payable 183.20.
        Assert.Equal(160.00m, semantic.LineNetTotal);
        Assert.Equal(23.20m, semantic.TaxTotal);
        Assert.Equal(183.20m, semantic.TaxInclusiveTotal);
        Assert.Equal(183.20m, semantic.PayableAmount);
        Assert.Equal(invoice.Lines.Count, semantic.Lines.Count);

        // The generator's own recognition of the XML it embedded agrees with the reopened document.
        var recognized = EInvoice.Recognize(created.Xml.ToArray()).SemanticData;
        Assert.NotNull(recognized);
        Assert.Equal(recognized!.TaxInclusiveTotal, semantic.TaxInclusiveTotal);
        Assert.Equal(recognized.TaxTotal, semantic.TaxTotal);
        Assert.Equal(recognized.InvoiceNumber, semantic.InvoiceNumber);
        Assert.Equal(recognized.IssueDate, semantic.IssueDate);
    }

    [Fact]
    public void The_designer_binds_to_every_canonical_path_the_generator_fills()
    {
        // The Designer projects the invoice through named binding paths. If one drifts, the page
        // silently shows nothing where a figure belongs.
        var bindings = Section(DesignerSource("features/e-invoice/invoiceModel.ts"), "EINVOICE_BINDINGS");

        foreach (var path in new[]
        {
            "invoice.invoiceNumber", "invoice.issueDate", "invoice.dueDate", "invoice.currency",
            "invoice.seller.name", "invoice.buyer.name",
            "line.identifier", "line.description", "line.quantity", "line.unitPrice",
            "line.taxRate", "line.netAmount",
            "tax.rate", "tax.amount",
            "totals.lineNetTotal", "totals.taxTotal", "totals.grandTotal"
        })
        {
            Assert.Contains($"'{path}'", bindings, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_migration_catalog_points_at_documentation_that_exists()
    {
        var guide = Documentation();
        foreach (var capability in Enum.GetValues<PdfEInvoiceCapability>())
        {
            var anchor = PdfEInvoiceMigrationCatalog.DocumentationFor(capability);
            var fragment = anchor[(anchor.IndexOf('#') + 1)..];
            // Every anchor resolves to a heading in the published guide.
            var heading = fragment.Replace('-', ' ');
            Assert.True(
                guide.Contains(fragment, StringComparison.OrdinalIgnoreCase)
                || guide.Contains(heading, StringComparison.OrdinalIgnoreCase),
                $"The guide has no section for '{fragment}'.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>The braced body of a named Designer constant, so a test reads what the UI declares.</summary>
    private static string Section(string source, string name)
    {
        var match = Regex.Match(source, Regex.Escape(name) + @"[^=]*=\s*(\{|\[)", RegexOptions.None);
        Assert.True(match.Success, $"{name} was not found in the Designer source.");
        var open = match.Groups[1].Value[0];
        var close = open == '{' ? '}' : ']';
        var depth = 0;
        for (var index = match.Groups[1].Index; index < source.Length; index++)
        {
            if (source[index] == open) depth++;
            else if (source[index] == close && --depth == 0)
                return source[match.Groups[1].Index..(index + 1)];
        }
        throw new InvalidOperationException($"{name} is not balanced.");
    }

    private static string DesignerSource(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "pvad-designer", "src", relativePath));

    private static string Documentation() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "guides", "PVAD.EInvoicing.md"));

    private static PvadEInvoiceOptions Options(PvadEInvoiceStandard standard) => new()
    {
        Standard = standard,
        Version = standard switch
        {
            PvadEInvoiceStandard.FacturX => EInvoice.CurrentFacturXVersion,
            PvadEInvoiceStandard.Zugferd => EInvoice.CurrentZugferdVersion,
            _ => EInvoice.CurrentXRechnungVersion
        },
        Profile = standard == PvadEInvoiceStandard.XRechnung ? "XRECHNUNG" : "EN 16931"
    };

    /// <summary>Two rates and two decimal places, so tax grouping and rounding both show up.</summary>
    private static PvadEInvoiceDocument Invoice() => new()
    {
        InvoiceNumber = "CROSS-SURFACE-1",
        IssueDate = new DateOnly(2026, 9, 27),
        DueDate = new DateOnly(2026, 10, 27),
        Currency = "EUR",
        BuyerReference = "04011000-12345-34",
        Seller = new("Seller GmbH", TaxIdentifier: "DE123456789",
            Address: new("Main Street 1", "Berlin", "10115", "DE")),
        Buyer = new("Buyer AG", TaxIdentifier: "DE987654321",
            Address: new("Buyer Street 2", "Munich", "80331", "DE")),
        Lines =
        [
            new("1", "Consulting", 1, "C62", 100m, 19m),
            new("2", "Handbook", 2, "C62", 30m, 7m)
        ]
    };

    private static FormFile FormFile(byte[] bytes, string name, string contentType) =>
        new(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private static string RepositoryRoot { get; } = Resolve();

    private static string Resolve()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the PXA repository root.");
    }
}
