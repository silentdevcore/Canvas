using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PVAD.Generator;
using PVAD.Pdf;

namespace PVAD.Generator.Tests;

/// <summary>
/// Generates the deterministic PDF/A validation inputs for the electronic-invoice evidence run.
/// </summary>
/// <remarks>
/// <para>
/// PDF/A conformance is checked by an external validator, so the inputs it is given have to be
/// reproducible or the evidence means nothing. This writes every fixture from a pinned synthetic
/// invoice, proves the bytes are identical across two generations in the same process, and pins each
/// one's SHA-256 in <c>test-assets/e-invoicing/pdfa-fixtures.json</c>. A generation change moves the
/// hash and fails here rather than silently invalidating a stored validator report.
/// </para>
/// <para>
/// The PDFs themselves are written to the ignored <c>artifacts/</c> tree rather than committed:
/// they are derived files, and the pinned hash is the stronger record. `tools/pdfa/einvoice-pdfa-evidence.mjs`
/// regenerates them through this test and hands them to the pinned veraPDF workflow.
/// </para>
/// <para>
/// veraPDF proves PDF/A properties only. It says nothing about Factur-X, ZUGFeRD or XRechnung
/// business conformance, which needs the official artifact sets PXA does not ship.
/// </para>
/// </remarks>
public sealed class EInvoicePdfaFixtureTests
{
    private const string ManifestSchema = "pxa.e-invoice.pdfa-fixtures/1";

    /// <summary>One PDF/A validation input.</summary>
    private sealed record Fixture(
        string Id,
        string Description,
        PvadEInvoiceStandard Standard,
        string Profile,
        /// <summary>The PDF/A flavour the file is expected to be judged against.</summary>
        string PdfaFlavour,
        /// <summary>"compliant" or "nonCompliant" - what the validator should conclude.</summary>
        string ExpectedResult,
        Func<PvadEInvoiceDocument> Build,
        /// <summary>True for the control fixture, which is written without a PDF/A profile.</summary>
        bool BreakCompliance = false);

    private static readonly Fixture[] Fixtures =
    [
        new("factur-x-single-page",
            "A Factur-X EN 16931 hybrid invoice on one page, at the standard German rate.",
            PvadEInvoiceStandard.FacturX, "EN 16931", "3b", "compliant",
            () => Invoice("PDFA-FX-0001", [Line(1, "Consulting engagement", 1, 100m, 19m)])),

        new("zugferd-single-page",
            "The same shape issued as ZUGFeRD, which shares the Factur-X CII syntax.",
            PvadEInvoiceStandard.Zugferd, "EN 16931", "3b", "compliant",
            () => Invoice("PDFA-ZF-0001", [Line(1, "Consulting engagement", 1, 100m, 19m)])),

        new("xrechnung-single-page",
            "An XRechnung hybrid invoice, which carries UBL rather than CII.",
            PvadEInvoiceStandard.XRechnung, "XRECHNUNG", "3b", "compliant",
            () => Invoice("PDFA-XR-0001", [Line(1, "Consulting engagement", 1, 100m, 19m)],
                buyerReference: "04011000-12345-34")),

        new("factur-x-multi-page",
            "Sixty lines, so the line table runs past the first page and the totals follow it.",
            PvadEInvoiceStandard.FacturX, "EN 16931", "3b", "compliant",
            () => Invoice("PDFA-FX-0002",
                [.. Enumerable.Range(1, 60).Select(index =>
                    Line(index, $"Position {index} - maintenance and support", 1, 10m + index, 19m))])),

        new("factur-x-long-description",
            "One description longer than a page, which the renderer continues as fragments.",
            PvadEInvoiceStandard.FacturX, "EN 16931", "3b", "compliant",
            () => Invoice("PDFA-FX-0003",
            [
                Line(1, "Implementation workshop", 2, 890m, 19m),
                Line(2, LongDescription(), 1, 4200m, 19m),
                Line(3, "Closing report", 1, 150m, 7m)
            ])),

        new("factur-x-many-tax-groups",
            "Twenty-seven distinct rates, so the tax breakdown continues across totals pages.",
            PvadEInvoiceStandard.FacturX, "EN 16931", "3b", "compliant",
            () => Invoice("PDFA-FX-0004",
                [.. ManyRates!.Select((rate, index) =>
                    Line(index + 1, $"Cross-border service at {rate}%", 1, 25m + index * 3.5m, rate,
                        rate == 0m ? "Z" : "S"))])),

        new("control-not-pdfa",
            "The control: the same invoice written without a PDF/A profile, so a validator that "
            + "reports it compliant is not actually validating anything.",
            PvadEInvoiceStandard.FacturX, "EN 16931", "3b", "nonCompliant",
            () => Invoice("PDFA-CTL-0001", [Line(1, "Consulting engagement", 1, 100m, 19m)]),
            BreakCompliance: true)
    ];

    private static IReadOnlyList<decimal> ManyRates { get; } =
    [
        0m, 2.1m, 3m, 4m, 5m, 5.5m, 6m, 7m, 8m, 9m, 10m, 12m, 13m, 13.5m, 14m,
        15m, 16m, 17m, 18m, 19m, 20m, 21m, 22m, 23m, 24m, 25m, 27m
    ];

    [Fact]
    public void Every_fixture_is_byte_identical_when_generated_twice()
    {
        foreach (var fixture in Fixtures)
        {
            var first = Render(fixture);
            var second = Render(fixture);
            Assert.True(
                first.AsSpan().SequenceEqual(second),
                $"{fixture.Id} is not deterministic; a stored validator report could not be trusted.");
        }
    }

    [Fact]
    public void The_fixtures_and_their_manifest_are_written_and_pinned()
    {
        var outputDirectory = Path.Combine(RepositoryRoot, "artifacts", "e-invoicing", "pdfa");
        Directory.CreateDirectory(outputDirectory);

        var entries = new JsonArray();
        foreach (var fixture in Fixtures)
        {
            var bytes = Render(fixture);
            var fileName = $"{fixture.Id}.pdf";
            File.WriteAllBytes(Path.Combine(outputDirectory, fileName), bytes);

            entries.Add(new JsonObject
            {
                ["id"] = fixture.Id,
                ["path"] = fileName,
                ["sha256"] = Sha256(bytes),
                ["sizeBytes"] = bytes.Length,
                ["provenance"] = "pxa-authored",
                ["license"] = "PXA test asset, redistributable with this repository",
                ["standard"] = fixture.Standard.ToString(),
                ["standardVersion"] = VersionFor(fixture.Standard),
                ["profile"] = fixture.Profile,
                ["pdfaFlavour"] = fixture.PdfaFlavour,
                ["expectedResult"] = fixture.ExpectedResult,
                ["generator"] = "PVAD.Generator EInvoice.CreatePdf",
                ["generatorVersion"] = ProductVersion,
                ["description"] = fixture.Description
            });
        }

        var manifest = new JsonObject
        {
            ["schema"] = ManifestSchema,
            ["description"] =
                "Deterministic PDF/A validation inputs for the electronic-invoice evidence run. Every "
                + "file is regenerated by EInvoicePdfaFixtureTests into the ignored artifacts/ tree; the "
                + "SHA-256 below is what the evidence runner checks before handing a file to veraPDF. "
                + "veraPDF proves PDF/A properties only and is not a Factur-X, ZUGFeRD or XRechnung "
                + "conformance claim.",
            ["provenanceKinds"] = new JsonArray("pxa-authored"),
            ["expectedResults"] = new JsonArray("compliant", "nonCompliant"),
            ["generatorVersion"] = ProductVersion,
            ["outputDirectory"] = "artifacts/e-invoicing/pdfa",
            ["fixtures"] = entries
        };

        var expected = manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
        var manifestPath = Path.Combine(
            RepositoryRoot, "test-assets", "e-invoicing", "pdfa-fixtures.json");
        var actual = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : string.Empty;

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            File.WriteAllText(manifestPath, expected);
            Assert.Fail($"{manifestPath} was out of date and has been regenerated. Review the diff.");
        }
    }

    [Fact]
    public void The_manifest_covers_every_required_case()
    {
        var ids = Fixtures.Select(static fixture => fixture.Id).ToArray();

        Assert.Contains(Fixtures, static fixture =>
            fixture.Standard == PvadEInvoiceStandard.FacturX && !fixture.BreakCompliance);
        Assert.Contains(Fixtures, static fixture => fixture.Standard == PvadEInvoiceStandard.Zugferd);
        Assert.Contains("factur-x-single-page", ids);
        Assert.Contains("factur-x-multi-page", ids);
        Assert.Contains("factur-x-long-description", ids);
        Assert.Contains("factur-x-many-tax-groups", ids);
        // Without a control, a validator that rubber-stamps everything would look like a pass.
        Assert.Contains(Fixtures, static fixture => fixture.ExpectedResult == "nonCompliant");
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_multi_page_and_overflow_fixtures_really_exercise_what_they_claim()
    {
        var multiPage = Fixtures.Single(static fixture => fixture.Id == "factur-x-multi-page");
        var plan = EInvoicePageLayout.Plan(
            [.. multiPage.Build().Lines.Select(static line => line.Description)], 1);
        Assert.True(plan.Pages.Count > 1, "The multi-page fixture fits on one page.");

        var longDescription = Fixtures.Single(static fixture => fixture.Id == "factur-x-long-description");
        var longPlan = EInvoicePageLayout.Plan(
            [.. longDescription.Build().Lines.Select(static line => line.Description)], 2);
        Assert.Contains(longPlan.Pages.SelectMany(static page => page.Rows),
            static row => row.FragmentIndex > 0);

        var manyTax = Fixtures.Single(static fixture => fixture.Id == "factur-x-many-tax-groups");
        Assert.True(manyTax.Build().Lines.Select(static line => line.TaxRate).Distinct().Count() >= 20);
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    private static byte[] Render(Fixture fixture)
    {
        var invoice = fixture.Build();
        var options = new PvadEInvoiceOptions
        {
            Standard = fixture.Standard,
            Version = VersionFor(fixture.Standard),
            Profile = fixture.Profile
        };

        if (!fixture.BreakCompliance)
        {
            var created = EInvoice.CreatePdf(invoice, options, FontLoader);
            Assert.True(created.IsGenerated, string.Join(
                " | ", created.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
            return created.Pdf.ToArray();
        }

        // The control writes the same content with no PDF/A profile, so the output is a valid PDF
        // that is deliberately not a PDF/A-3 document. Nothing is corrupted: an invalid file would
        // prove the validator rejects junk, not that it checks conformance.
        var generated = EInvoice.GenerateXml(invoice, options);
        Assert.True(generated.IsGenerated);
        var document = Pdf.CreateDocument();
        document.FontLoader = FontLoader;
        document.Language = "en";
        document.Info.Title = $"Invoice {invoice.InvoiceNumber}";
        var page = document.AddPage(EInvoicePageLayout.PageWidth, EInvoicePageLayout.PageHeight);
        page.DrawTextFromTop("INVOICE", EInvoicePageLayout.Left, 30, new PdfDrawTextOptions
        {
            FontSize = 20,
            Bold = true,
            FontFamily = PdfFontFamily.Helvetica,
            Language = "en"
        });
        return document.ToBytes(new PdfSaveOptions
        {
            ComplianceProfile = PdfComplianceProfile.None,
            CompressContentStreams = true
        });
    }

    // Lazy, because a static field initializer would run before RepositoryRoot is assigned.
    private static readonly Lazy<PdfFontLoader> LazyFontLoader = new(() =>
        new PdfFontLoader(Path.Combine(RepositoryRoot, "assets", "fonts", "liberation")));

    private static PdfFontLoader FontLoader => LazyFontLoader.Value;

    private static string VersionFor(PvadEInvoiceStandard standard) => standard switch
    {
        PvadEInvoiceStandard.FacturX => EInvoice.CurrentFacturXVersion,
        PvadEInvoiceStandard.Zugferd => EInvoice.CurrentZugferdVersion,
        _ => EInvoice.CurrentXRechnungVersion
    };

    // ── Invoice building ─────────────────────────────────────────────────────

    private static PvadEInvoiceDocument Invoice(
        string number,
        IReadOnlyList<PvadEInvoiceDocumentLine> lines,
        string? buyerReference = null) => new()
    {
        InvoiceNumber = number,
        IssueDate = new DateOnly(2026, 9, 27),
        DueDate = new DateOnly(2026, 10, 27),
        Currency = "EUR",
        BuyerReference = buyerReference,
        Seller = new("PXA Synthetic Seller GmbH", TaxIdentifier: "DE123456789",
            Address: new("Hauptstrasse 1", "Berlin", "10115", "DE")),
        Buyer = new("PXA Synthetic Buyer AG", TaxIdentifier: "DE987654321",
            Address: new("Buyer Street 2", "Munich", "80331", "DE")),
        Lines = lines
    };

    private static PvadEInvoiceDocumentLine Line(
        int position,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal taxRate,
        string category = "S") =>
        new(position.ToString(CultureInfo.InvariantCulture), description, quantity, "C62",
            unitPrice, taxRate, category);

    private static string LongDescription()
    {
        const string phrase = "annual maintenance, support and licence renewal for the consolidated "
            + "reporting platform including the quarterly compliance review";
        var builder = new StringBuilder();
        for (var index = 0; index < 24; index++)
        {
            if (index > 0) builder.Append(' ');
            builder.Append(phrase);
        }
        return builder.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string ProductVersion { get; } =
        typeof(EInvoice).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? typeof(EInvoice).Assembly.GetName().Version?.ToString(3)
        ?? "unknown";

    private static readonly Lazy<string> LazyRoot = new(Resolve);

    private static string RepositoryRoot => LazyRoot.Value;

    private static string Resolve()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the PXA repository root.");
    }
}
