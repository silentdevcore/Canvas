using System.Text;
using PXA.Generator;
using PXA.Importer.EInvoicing;
using PXA.Pdf;

namespace PXA.Api.Tests;

/// <summary>
/// The edges of every documented electronic-invoice limit, from repository-owned synthetic data.
/// </summary>
/// <remarks>
/// <para>
/// A limit is only real if both sides of it are tested: the largest accepted value and the smallest
/// rejected one. These are the pairs PXA can judge without any official artifact - description
/// length, Unicode segmentation, line count, currency minor units, rounding, attachment naming and
/// the reserved filenames.
/// </para>
/// <para>
/// Nothing here is a conformance claim, and no fixture comes from a customer. Where a shape is not
/// supported at all - credit notes, for instance - the test records the refusal rather than
/// pretending the feature exists.
/// </para>
/// </remarks>
public sealed class PdfEInvoiceBoundaryTests
{
    // ── Description length and Unicode segmentation ──────────────────────────

    [Fact]
    public void A_maximum_length_description_without_any_whitespace_is_accepted_and_kept_whole()
    {
        // One 4 096-character token. Nothing can wrap it at a space, so the renderer has to break
        // the token itself rather than clip it or refuse the invoice.
        var description = new string('A', 4_096);

        var generated = Generate(Invoice([Line(1, description)]));

        Assert.True(generated.IsGenerated);
        Assert.Contains(description, Encoding.UTF8.GetString(generated.Xml.Span), StringComparison.Ordinal);

        var wrapped = EInvoicePageLayout.WrapText(description, EInvoicePageLayout.Description.Width, 9);
        Assert.True(wrapped.Count > 1, "A 4 096-character token must be broken across lines.");
        // Broken, never shortened.
        Assert.Equal(description, string.Concat(wrapped));
    }

    [Fact]
    public void A_description_of_astral_scalars_survives_wrapping_intact()
    {
        // Every scalar is a surrogate pair, so a naive UTF-16 slice would cut one in half and emit
        // a replacement character. The break has to fall on a code-point boundary.
        var description = string.Concat(Enumerable.Repeat("\U0001F9FE", 2_048));

        var wrapped = EInvoicePageLayout.WrapText(description, EInvoicePageLayout.Description.Width, 9);
        var rejoined = string.Concat(wrapped);

        Assert.Equal(description, rejoined);
        Assert.DoesNotContain('�', rejoined);
        foreach (var line in wrapped)
        {
            Assert.False(char.IsLowSurrogate(line[0]), "A fragment began with a trailing surrogate.");
            Assert.False(char.IsHighSurrogate(line[^1]), "A fragment ended with a leading surrogate.");
        }
    }

    [Fact]
    public void A_mixed_script_description_keeps_every_scalar()
    {
        var description = string.Concat(Enumerable.Repeat("Größe-日本語-\U0001F9FE-Ω", 200));

        var rejoined = string.Concat(
            EInvoicePageLayout.WrapText(description, EInvoicePageLayout.Description.Width, 9));

        Assert.Equal(description.Replace(" ", string.Empty), rejoined.Replace(" ", string.Empty));
        Assert.DoesNotContain('�', rejoined);
    }

    [Fact]
    public void One_character_above_the_description_limit_is_refused()
    {
        var generated = Generate(Invoice([Line(1, new string('A', 4_097))]));

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV109");
    }

    [Fact]
    public void The_description_limit_counts_utf16_units_consistently_at_both_edges()
    {
        Assert.True(Generate(Invoice([Line(1, new string('A', 4_096))])).IsGenerated);
        Assert.False(Generate(Invoice([Line(1, new string('A', 4_097))])).IsGenerated);
    }

    // ── Line count ───────────────────────────────────────────────────────────

    [Fact]
    public void The_maximum_supported_line_count_is_accepted()
    {
        var lines = Enumerable.Range(1, 10_000).Select(index => Line(index, "Unit")).ToArray();

        var generated = Generate(Invoice(lines));

        Assert.True(generated.IsGenerated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV107");
    }

    [Fact]
    public void One_line_beyond_the_synchronous_limit_is_refused()
    {
        var lines = Enumerable.Range(1, 10_001).Select(index => Line(index, "Unit")).ToArray();

        var generated = Generate(Invoice(lines));

        Assert.False(generated.IsGenerated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Code == "PXAEINV107");
    }

    // ── Currency minor units and rounding ────────────────────────────────────

    [Theory]
    [InlineData(0, "100", "19", "119")]
    [InlineData(2, "100.00", "19.00", "119.00")]
    [InlineData(3, "100.000", "19.000", "119.000")]
    public void Every_supported_minor_unit_count_reaches_the_xml_with_its_own_digits(
        int places,
        string net,
        string tax,
        string payable)
    {
        var generated = Generate(Invoice([Line(1, "Consulting", 1, 100m)]) with
        {
            CurrencyDecimalPlaces = places
        });

        Assert.True(generated.IsGenerated);
        var xml = Encoding.UTF8.GetString(generated.Xml.Span);
        Assert.Contains($"<ram:LineTotalAmount>{net}</ram:LineTotalAmount>", xml, StringComparison.Ordinal);
        Assert.Contains($">{tax}</ram:TaxTotalAmount>", xml, StringComparison.Ordinal);
        Assert.Contains($"<ram:GrandTotalAmount>{payable}</ram:GrandTotalAmount>", xml, StringComparison.Ordinal);
    }

    [Theory]
    // Exactly half a minor unit, which .NET's default banker's rounding would take the other way.
    [InlineData(0.125, "0.13")]
    [InlineData(0.135, "0.14")]
    [InlineData(0.145, "0.15")]
    [InlineData(2.005, "2.01")]
    public void A_half_minor_unit_rounds_away_from_zero(decimal unitPrice, string expected)
    {
        var generated = Generate(Invoice([Line(1, "Rounding", 1, unitPrice)]));

        Assert.True(generated.IsGenerated);
        Assert.Contains(
            $"<ram:LineTotalAmount>{expected}</ram:LineTotalAmount>",
            Encoding.UTF8.GetString(generated.Xml.Span),
            StringComparison.Ordinal);
    }

    // ── Credit notes ─────────────────────────────────────────────────────────

    [Fact]
    public void Negative_line_values_are_refused_because_credit_notes_are_not_supported()
    {
        // PXA issues invoices (type code 380) only. A negative price or quantity would silently
        // produce something that is not a credit note either, so both are refused outright.
        Assert.Contains(
            Generate(Invoice([Line(1, "Credit", 1, -100m)])).Diagnostics,
            diagnostic => diagnostic.Code == "PXAEINV114");
        Assert.Contains(
            Generate(Invoice([Line(1, "Credit", -1m, 100m)])).Diagnostics,
            diagnostic => diagnostic.Code == "PXAEINV113");
    }

    [Fact]
    public void Every_generated_invoice_declares_the_invoice_type_code()
    {
        Assert.Contains(
            "<ram:TypeCode>380</ram:TypeCode>",
            Encoding.UTF8.GetString(Generate(Invoice([Line(1, "Consulting")])).Xml.Span),
            StringComparison.Ordinal);
    }

    // ── Attachment naming ────────────────────────────────────────────────────

    [Theory]
    [InlineData(PxaEInvoiceStandard.FacturX, "zugferd-invoice.xml")]
    [InlineData(PxaEInvoiceStandard.XRechnung, "factur-x.xml")]
    public void An_attachment_name_that_is_not_the_reserved_one_is_refused(
        PxaEInvoiceStandard standard,
        string fileName)
    {
        // A recipient looks for the reserved name. Writing the invoice under another one produces a
        // document whose invoice nothing will find.
        var created = EInvoice.CreatePdf(Invoice([Line(1, "Consulting")]), new PxaEInvoiceOptions
        {
            Standard = standard,
            Version = VersionFor(standard),
            Profile = standard == PxaEInvoiceStandard.XRechnung ? "XRECHNUNG" : "EN 16931",
            FileName = fileName
        }, Fonts);

        Assert.False(created.IsGenerated);
        Assert.Contains(created.Diagnostics, diagnostic => diagnostic.Severity == "error");
    }

    [Theory]
    [InlineData("../factur-x.xml")]
    [InlineData("/etc/factur-x.xml")]
    [InlineData("factur-x.xml\0.exe")]
    [InlineData("fact\\ur-x.xml")]
    public void An_unsafe_attachment_filename_is_refused(string fileName)
    {
        var created = EInvoice.CreatePdf(Invoice([Line(1, "Consulting")]), new PxaEInvoiceOptions
        {
            Standard = PxaEInvoiceStandard.FacturX,
            Version = EInvoice.CurrentFacturXVersion,
            Profile = "EN 16931",
            FileName = fileName
        }, Fonts);

        Assert.False(created.IsGenerated);
        Assert.Contains(created.Diagnostics, diagnostic => diagnostic.Severity == "error");
    }

    [Fact]
    public void The_reserved_name_is_accepted_for_the_standard_that_owns_it()
    {
        var created = EInvoice.CreatePdf(Invoice([Line(1, "Consulting")]), new PxaEInvoiceOptions
        {
            Standard = PxaEInvoiceStandard.FacturX,
            Version = EInvoice.CurrentFacturXVersion,
            Profile = "EN 16931",
            FileName = EInvoice.GetDefaultFileName(PxaEInvoiceStandard.FacturX)
        }, Fonts);

        Assert.True(created.IsGenerated);
    }

    [Fact]
    public async Task A_generated_invoice_carries_exactly_one_attachment_under_the_reserved_name()
    {
        var created = EInvoice.CreatePdf(Invoice([Line(1, "Consulting")]), Options(), Fonts);
        Assert.True(created.IsGenerated);

        using var stream = new MemoryStream(created.Pdf.ToArray(), writable: false);
        var files = await new PdfAssociatedFileExtractor().ExtractAsync(stream);

        // Two entries under the reserved name would leave a recipient no way to tell which invoice
        // governs, so generation must never produce a duplicate.
        var attachment = Assert.Single(files);
        Assert.Equal("factur-x.xml", attachment.FileName);
        Assert.True(attachment.SizeMatches);
        Assert.True(attachment.ChecksumMatches);
        Assert.Single(files, file => string.Equals(file.FileName, "factur-x.xml", StringComparison.Ordinal));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PxaEInvoiceGenerationResult Generate(PxaEInvoiceDocument invoice) =>
        EInvoice.GenerateXml(invoice, Options());

    private static PxaEInvoiceOptions Options() => new()
    {
        Standard = PxaEInvoiceStandard.FacturX,
        Version = EInvoice.CurrentFacturXVersion,
        Profile = "EN 16931"
    };

    private static string VersionFor(PxaEInvoiceStandard standard) => standard switch
    {
        PxaEInvoiceStandard.FacturX => EInvoice.CurrentFacturXVersion,
        PxaEInvoiceStandard.Zugferd => EInvoice.CurrentZugferdVersion,
        _ => EInvoice.CurrentXRechnungVersion
    };

    private static PxaEInvoiceDocument Invoice(IReadOnlyList<PxaEInvoiceDocumentLine> lines) => new()
    {
        InvoiceNumber = "BOUNDARY-1",
        IssueDate = new DateOnly(2026, 9, 27),
        Currency = "EUR",
        Seller = new("PXA Synthetic Seller GmbH", Address: new("Hauptstrasse 1", "Berlin", "10115", "DE")),
        Buyer = new("PXA Synthetic Buyer AG", Address: new("Buyer Street 2", "Munich", "80331", "DE")),
        Lines = lines
    };

    private static PxaEInvoiceDocumentLine Line(
        int position,
        string description,
        decimal quantity = 1,
        decimal unitPrice = 100m) =>
        new(position.ToString(), description, quantity, "C62", unitPrice, 19m);

    private static PdfFontLoader Fonts { get; } =
        new(Path.Combine(AppContext.BaseDirectory, "fonts"));
}
