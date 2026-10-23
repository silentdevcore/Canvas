using System.Globalization;
using System.Text;
using System.Xml.Linq;
using PVAD.Pdf;

namespace PVAD.Generator;

public sealed record PvadEInvoiceAddress(
    string Street,
    string City,
    string PostalCode,
    string CountryCode,
    string? AdditionalLine = null);

public sealed record PvadEInvoiceBusinessParty(
    string Name,
    string? Identifier = null,
    string? TaxIdentifier = null,
    string? EndpointIdentifier = null,
    PvadEInvoiceAddress? Address = null);

public sealed record PvadEInvoiceDocumentLine(
    string Identifier,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    decimal TaxRate,
    string TaxCategoryCode = "S");

public sealed record PvadEInvoiceDocument
{
    public required string InvoiceNumber { get; init; }
    public required DateOnly IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public required string Currency { get; init; }
    public required PvadEInvoiceBusinessParty Seller { get; init; }
    public required PvadEInvoiceBusinessParty Buyer { get; init; }
    public required IReadOnlyList<PvadEInvoiceDocumentLine> Lines { get; init; }
    public string? BuyerReference { get; init; }
    public string? PaymentReference { get; init; }
    public int CurrencyDecimalPlaces { get; init; } = 2;
}

public sealed record PvadEInvoiceGenerationResult(
    bool IsGenerated,
    ReadOnlyMemory<byte> Xml,
    PvadEInvoiceSemanticData? SemanticData,
    IReadOnlyList<PvadEInvoiceDiagnostic> Diagnostics);

public sealed record PvadEInvoicePdfGenerationResult(
    bool IsGenerated,
    ReadOnlyMemory<byte> Pdf,
    ReadOnlyMemory<byte> Xml,
    PvadEInvoiceSemanticData? SemanticData,
    IReadOnlyList<PvadEInvoiceDiagnostic> Diagnostics);

public static partial class EInvoice
{
    private static readonly XNamespace CiiRoot =
        "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
    private static readonly XNamespace CiiAggregate =
        "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationEntity:100";
    private static readonly XNamespace CiiUnqualified =
        "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";
    private static readonly XNamespace UblInvoice =
        "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace UblBasic =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace UblAggregate =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public static PvadEInvoiceGenerationResult GenerateXml(
        PvadEInvoiceDocument invoice,
        PvadEInvoiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(options);
        var diagnostics = new List<PvadEInvoiceDiagnostic>();
        ValidateOptions(options, diagnostics);
        ValidateDocument(invoice, options.Standard, diagnostics);
        if (diagnostics.Any(static diagnostic => diagnostic.Severity == "error"))
            return new(false, ReadOnlyMemory<byte>.Empty, null, diagnostics);

        var calculations = Calculate(invoice);
        var document = options.Standard == PvadEInvoiceStandard.XRechnung
            ? CreateUblInvoice(invoice, calculations)
            : CreateCiiInvoice(invoice, options, calculations);
        var xml = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        var recognition = Recognize(xml);
        diagnostics.AddRange(recognition.Diagnostics);
        if (!recognition.IsRecognized)
            diagnostics.Add(Error("PXAEINV119", "Generated invoice XML failed structural recognition."));

        return new(
            diagnostics.All(static diagnostic => diagnostic.Severity != "error"),
            xml,
            recognition.SemanticData,
            diagnostics);
    }

    public static PvadEInvoicePdfGenerationResult CreatePdf(
        PvadEInvoiceDocument invoice,
        PvadEInvoiceOptions options,
        PdfFontLoader? fontLoader)
    {
        var generated = GenerateXml(invoice, options);
        if (!generated.IsGenerated)
            return new(false, ReadOnlyMemory<byte>.Empty, generated.Xml, generated.SemanticData, generated.Diagnostics);
        if (fontLoader is null)
        {
            return new(false, ReadOnlyMemory<byte>.Empty, generated.Xml, generated.SemanticData,
                [.. generated.Diagnostics, Error("PXAEINV120", "PDF/A-3 invoice creation requires an embedded-font loader.")]);
        }

        var diagnostics = generated.Diagnostics.ToList();
        try
        {
            var calculations = Calculate(invoice);
            var plan = EInvoicePageLayout.Plan(
                [.. invoice.Lines.Select(static line => line.Description)],
                calculations.Taxes.Count);
            if (plan.OverflowingLines.Count > 0)
            {
                // Fail closed rather than emit a document of unbounded length. A description this
                // long cannot reach here through the public API: PXAEINV109 caps it well below the
                // bound, so this guards the renderer itself.
                var positions = string.Join(", ", plan.OverflowingLines.Select(static index => index + 1));
                return new(false, ReadOnlyMemory<byte>.Empty, generated.Xml, generated.SemanticData,
                    [.. diagnostics, Error("PXAEINV122",
                        $"Invoice line {positions} has a description too long to render within {EInvoicePageLayout.MaximumDescriptionFragments} continuation fragments.")]);
            }
            var document = Pdf.CreateDocument();
            document.FontLoader = fontLoader;
            document.Language = "en";
            document.Info.Title = $"Invoice {invoice.InvoiceNumber.Trim()}";
            document.Info.Subject = $"{options.Standard} electronic invoice";
            RenderInvoice(document, invoice, calculations, plan);
            Attach(document, generated.Xml, options);
            var pdf = document.ToBytes(new PdfSaveOptions
            {
                ComplianceProfile = PdfComplianceProfile.PdfA3B,
                CompressContentStreams = true,
                CollectDiagnostics = true
            });
            return new(true, pdf, generated.Xml, generated.SemanticData, diagnostics);
        }
        catch (Exception exception) when (exception is PdfComplianceException or InvalidDataException or ArgumentException)
        {
            diagnostics.Add(Error("PXAEINV121", $"The PDF/A-3 invoice could not be created: {exception.Message}"));
            return new(false, ReadOnlyMemory<byte>.Empty, generated.Xml, generated.SemanticData, diagnostics);
        }
    }

    public static string GetDefaultFileName(PvadEInvoiceStandard standard) => DefaultFileName(standard);

    /// <summary>
    /// Draws the invoice onto as many pages as <see cref="EInvoicePageLayout"/> plans for it.
    /// </summary>
    /// <remarks>
    /// Geometry, pagination and wording all come from the shared contract, so this renderer and the
    /// Designer projection put the same content on the same pages. Nothing here decides a layout of
    /// its own.
    /// </remarks>
    private static void RenderInvoice(
        PdfDocument document,
        PvadEInvoiceDocument invoice,
        InvoiceCalculations calculations,
        EInvoicePageLayout.InvoicePlan plan)
    {
        var currency = invoice.Currency.Trim().ToUpperInvariant();
        var places = invoice.CurrencyDecimalPlaces;

        foreach (var planned in plan.Pages)
        {
            var page = NewInvoicePage(document, invoice, planned.PageNumber, planned.IsFirst);

            if (planned.TableTop is { } tableTop && planned.Rows.Count > 0)
                DrawTableHeader(page, tableTop);

            foreach (var row in planned.Rows)
                DrawInvoiceLine(page, calculations.Lines[row.LineIndex], row, currency, places);

            if (planned.TotalsTop is { } totalsTop)
                DrawTotals(page, calculations, planned, currency, places, totalsTop);

            page.DrawTextBoxFromTop(ConformanceNote, EInvoicePageLayout.Left,
                EInvoicePageLayout.FooterTop, EInvoicePageLayout.Right - EInvoicePageLayout.Left, 12,
                Box(7, color: Muted));
        }
    }

    /// <summary>The same standing note the maintained Designer layout carries in its footer.</summary>
    private const string ConformanceNote =
        "The embedded invoice XML is the legally relevant data. Structural recognition by PVAD is not a conformance claim.";

    private static readonly PdfColor Muted = PdfColor.FromRgb(100, 116, 139);
    private static readonly PdfColor Ink = PdfColor.FromRgb(15, 23, 42);
    private static readonly PdfColor Hairline = PdfColor.FromRgb(148, 163, 184);

    private static PdfPage NewInvoicePage(
        PdfDocument document,
        PvadEInvoiceDocument invoice,
        int pageNumber,
        bool showParties)
    {
        var page = document.AddPage(EInvoicePageLayout.PageWidth, EInvoicePageLayout.PageHeight);
        page.DrawTextFromTop("INVOICE", EInvoicePageLayout.Left, 30, Text(20, bold: true));
        DrawRight(page, invoice.InvoiceNumber.Trim(), 350, 32, 203, 12, bold: true);
        page.DrawLineFromTop(EInvoicePageLayout.Left, 62, EInvoicePageLayout.Right, 62, 1, Hairline);
        if (!showParties) return page;

        DrawParty(page, "Seller", invoice.Seller, EInvoicePageLayout.Left, 220, 106, 156);
        DrawParty(page, "Buyer", invoice.Buyer, 310, 243, 374, 179);

        page.DrawTextFromTop("Issue date", EInvoicePageLayout.Left, 190, Text(9, color: Muted));
        page.DrawTextFromTop(invoice.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EInvoicePageLayout.Left + 94, 190, Text(9));
        if (invoice.DueDate is { } dueDate)
        {
            page.DrawTextFromTop("Due date", EInvoicePageLayout.Left, 206, Text(9, color: Muted));
            page.DrawTextFromTop(dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EInvoicePageLayout.Left + 94, 206, Text(9));
        }
        if (!string.IsNullOrWhiteSpace(invoice.BuyerReference))
        {
            page.DrawTextFromTop("Buyer reference", 310, 190, Text(9, color: Muted));
            page.DrawTextFromTop(invoice.BuyerReference.Trim(), 424, 190, Text(9));
        }
        if (!string.IsNullOrWhiteSpace(invoice.PaymentReference))
        {
            page.DrawTextFromTop("Payment reference", 310, 206, Text(9, color: Muted));
            page.DrawTextFromTop(invoice.PaymentReference.Trim(), 424, 206, Text(9));
        }
        return page;
    }

    private static void DrawParty(
        PdfPage page,
        string heading,
        PvadEInvoiceBusinessParty party,
        double x,
        double width,
        double cityX,
        double cityWidth)
    {
        page.DrawTextFromTop(heading, x, 78, Text(9, bold: true, color: Muted));
        page.DrawTextFromTop(party.Name.Trim(), x, 94, Text(9, bold: true));
        if (party.Address is { } address)
        {
            page.DrawTextFromTop(address.Street.Trim(), x, 110, Text(9));
            page.DrawTextFromTop(address.PostalCode.Trim(), x, 126, Text(9));
            page.DrawTextBoxFromTop(address.City.Trim(), cityX, 126, cityWidth, 14, Box(9));
            page.DrawTextFromTop(address.CountryCode.Trim().ToUpperInvariant(), x, 142, Text(9));
        }
        if (!string.IsNullOrWhiteSpace(party.TaxIdentifier))
            page.DrawTextBoxFromTop(party.TaxIdentifier.Trim(), x, 158, width, 12, Box(8));
    }

    private static void DrawTableHeader(PdfPage page, double tableTop)
    {
        var top = tableTop - EInvoicePageLayout.ColumnHeaderOffset;
        DrawHeading(page, "No.", EInvoicePageLayout.Position, top, right: false);
        DrawHeading(page, "Description", EInvoicePageLayout.Description, top, right: false);
        DrawHeading(page, "Quantity", EInvoicePageLayout.Quantity, top, right: true);
        DrawHeading(page, "Unit price", EInvoicePageLayout.UnitPrice, top, right: true);
        DrawHeading(page, "Tax", EInvoicePageLayout.TaxRate, top, right: true);
        DrawHeading(page, "Net", EInvoicePageLayout.Net, top, right: true);
        var ruleTop = tableTop - EInvoicePageLayout.HeaderRuleOffset;
        page.DrawLineFromTop(EInvoicePageLayout.Left, ruleTop, EInvoicePageLayout.Right, ruleTop, 1, Hairline);
    }

    private static void DrawHeading(
        PdfPage page,
        string text,
        EInvoicePageLayout.Column column,
        double top,
        bool right) =>
        page.DrawTextBoxFromTop(text, column.X, top, column.Width, 14,
            Box(9, bold: true, color: Muted, right: right));

    private static void DrawInvoiceLine(
        PdfPage page,
        CalculatedLine line,
        EInvoicePageLayout.PlannedRow row,
        string currency,
        int decimalPlaces)
    {
        // A continuation fragment carries only the rest of the description. Repeating the amounts
        // would show the same money twice for one canonical line.
        page.DrawTextBoxFromTop(row.Text, EInvoicePageLayout.Description.X, row.Top,
            EInvoicePageLayout.Description.Width,
            Math.Max(14, row.TextLines * EInvoicePageLayout.LineHeight),
            Box(9, lineHeight: EInvoicePageLayout.LineHeight));
        if (row.FragmentIndex > 0) return;

        page.DrawTextBoxFromTop(line.Source.Identifier.Trim(), EInvoicePageLayout.Position.X, row.Top,
            EInvoicePageLayout.Position.Width, 14, Box(9));
        DrawColumn(page, Decimal(line.Source.Quantity), EInvoicePageLayout.Quantity, row.Top);
        DrawColumn(page, Money(line.Source.UnitPrice, decimalPlaces), EInvoicePageLayout.UnitPrice, row.Top);
        DrawColumn(page, $"{Decimal(line.Source.TaxRate)}%", EInvoicePageLayout.TaxRate, row.Top);
        DrawColumn(page, Money(line.NetAmount, decimalPlaces), EInvoicePageLayout.Net, row.Top);
    }

    private static void DrawColumn(
        PdfPage page,
        string text,
        EInvoicePageLayout.Column column,
        double top) =>
        page.DrawTextBoxFromTop(text, column.X, top, column.Width, 14, Box(9, right: true));

    /// <summary>
    /// Draws whichever part of the totals block this page carries.
    /// </summary>
    /// <remarks>
    /// A large tax breakdown continues across several totals pages. The net total is drawn once, on
    /// the first of them, and the payable total once, on the last, so neither is ever duplicated and
    /// neither runs into the footer.
    /// </remarks>
    private static void DrawTotals(
        PdfPage page,
        InvoiceCalculations calculations,
        EInvoicePageLayout.PlannedPage planned,
        string currency,
        int decimalPlaces,
        double top)
    {
        if (planned.ShowNetTotal)
        {
            DrawTotalsRow(page, "Net total", Money(calculations.LineTotal, decimalPlaces), top, 9, bold: false);
        }

        var taxTop = planned.ShowNetTotal ? top + 18 : top;
        var taxes = calculations.Taxes
            .Skip(planned.TaxGroupStart)
            .Take(planned.TaxGroupCount)
            .ToArray();
        for (var index = 0; index < taxes.Length; index++)
        {
            var tax = taxes[index];
            DrawTotalsRow(page, $"{Decimal(tax.Rate)}%", Money(tax.Amount, decimalPlaces),
                taxTop + index * EInvoicePageLayout.TaxRowHeight, 9, bold: false);
        }

        if (!planned.ShowPayableTotal) return;

        var ruleTop = taxTop + Math.Max(taxes.Length, 1) * EInvoicePageLayout.TaxRowHeight;

        // With one group the group row already is the tax total; with several the aggregate needs
        // its own row, or nothing on the page confirms the total the embedded XML declares - and
        // the PDF/XML consistency comparison would report the invoice for review.
        if (calculations.Taxes.Count > 1)
        {
            DrawTotalsRow(page, "Tax total", Money(calculations.TaxTotal, decimalPlaces),
                ruleTop, 9, bold: false);
            ruleTop += EInvoicePageLayout.TaxRowHeight;
        }

        page.DrawLineFromTop(EInvoicePageLayout.TotalsLabel.X, ruleTop,
            EInvoicePageLayout.Net.Right, ruleTop, 1, Hairline);
        var payableTop = ruleTop + 10;
        DrawTotalsRow(page, "Amount payable", Money(calculations.GrandTotal, decimalPlaces),
            payableTop, 11, bold: true);
        page.DrawTextBoxFromTop(currency, EInvoicePageLayout.Currency.X, payableTop,
            EInvoicePageLayout.Currency.Width, 16, Box(11, bold: true));
    }

    private static void DrawTotalsRow(
        PdfPage page,
        string label,
        string amount,
        double top,
        double size,
        bool bold)
    {
        page.DrawTextBoxFromTop(label, EInvoicePageLayout.TotalsLabel.X, top,
            EInvoicePageLayout.TotalsLabel.Width, size + 5, Box(size, bold: bold, right: true));
        page.DrawTextBoxFromTop(amount, EInvoicePageLayout.Net.X, top,
            EInvoicePageLayout.Net.Width, size + 5, Box(size, bold: bold, right: true));
    }

    private static void DrawRight(
        PdfPage page,
        string text,
        double x,
        double top,
        double width,
        double size,
        bool bold) =>
        page.DrawTextBoxFromTop(text, x, top, width, size + 5, Box(size, bold: bold, right: true));

    /// <summary>
    /// A text box in the contract's typeface.
    /// </summary>
    /// <remarks>
    /// The family is named explicitly because the weight depends on it: with no family the page
    /// falls back to its default font and drops <c>Bold</c> silently, so a bold heading rendered
    /// regular. Helvetica is also the slot the Designer's maintained layouts ask for, which the font
    /// loader backs with Liberation Sans on both surfaces.
    /// </remarks>
    private static PdfTextBoxOptions Box(
        double size,
        bool bold = false,
        IPdfColor? color = null,
        bool right = false,
        double? lineHeight = null) => new()
    {
        FontSize = size,
        Bold = bold,
        FontFamily = PdfFontFamily.Helvetica,
        FillColor = color ?? Ink,
        Alignment = right ? PdfTextAlignment.Right : PdfTextAlignment.Left,
        LineHeight = lineHeight,
        Language = "en"
    };

    /// <summary>A single line of text in the contract's typeface. See <see cref="Box"/> on the family.</summary>
    private static PdfDrawTextOptions Text(double size, bool bold = false, IPdfColor? color = null) => new()
    {
        FontSize = size,
        Bold = bold,
        FontFamily = PdfFontFamily.Helvetica,
        FillColor = color ?? Ink,
        Language = "en"
    };

    private static void ValidateDocument(
        PvadEInvoiceDocument invoice,
        PvadEInvoiceStandard standard,
        ICollection<PvadEInvoiceDiagnostic> diagnostics)
    {
        Required(invoice.InvoiceNumber, 200, "PXAEINV100", "An invoice number is required.", diagnostics);
        Required(invoice.Currency, 3, "PXAEINV101", "A three-letter currency code is required.", diagnostics);
        if (!string.IsNullOrWhiteSpace(invoice.Currency) &&
            (invoice.Currency.Trim().Length != 3 || !invoice.Currency.Trim().All(char.IsLetter)))
            diagnostics.Add(Error("PXAEINV102", "The currency must be a three-letter alphabetic code."));
        if (invoice.CurrencyDecimalPlaces is < 0 or > 4)
            diagnostics.Add(Error("PXAEINV103", "Currency decimal places must be between zero and four."));
        ValidateParty(invoice.Seller, "seller", "PXAEINV104", diagnostics);
        ValidateParty(invoice.Buyer, "buyer", "PXAEINV105", diagnostics);
        if (invoice.Lines is null || invoice.Lines.Count == 0)
            diagnostics.Add(Error("PXAEINV106", "At least one invoice line is required."));
        else if (invoice.Lines.Count > 10_000)
            diagnostics.Add(Error("PXAEINV107", "An invoice cannot contain more than 10,000 lines."));
        else
        {
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (line, index) in invoice.Lines.Select(static (line, index) => (line, index)))
            {
                Required(line.Identifier, 200, "PXAEINV108", $"Invoice line {index + 1} requires an identifier.", diagnostics);
                Required(line.Description, 4_096, "PXAEINV109", $"Invoice line {index + 1} requires a description.", diagnostics);
                Required(line.UnitCode, 16, "PXAEINV110", $"Invoice line {index + 1} requires a unit code.", diagnostics);
                Required(line.TaxCategoryCode, 16, "PXAEINV111", $"Invoice line {index + 1} requires a tax category.", diagnostics);
                if (!string.IsNullOrWhiteSpace(line.Identifier) && !identifiers.Add(line.Identifier.Trim()))
                    diagnostics.Add(Error("PXAEINV112", $"Invoice line identifier '{line.Identifier.Trim()}' is duplicated."));
                if (line.Quantity <= 0)
                    diagnostics.Add(Error("PXAEINV113", $"Invoice line {index + 1} quantity must be greater than zero."));
                if (line.UnitPrice < 0)
                    diagnostics.Add(Error("PXAEINV114", $"Invoice line {index + 1} unit price cannot be negative."));
                if (line.TaxRate is < 0 or > 100)
                    diagnostics.Add(Error("PXAEINV115", $"Invoice line {index + 1} tax rate must be between zero and 100."));
            }
        }

        if (invoice.DueDate is { } dueDate && dueDate < invoice.IssueDate)
            diagnostics.Add(Error("PXAEINV116", "The due date cannot be earlier than the issue date."));
        if (standard == PvadEInvoiceStandard.XRechnung && string.IsNullOrWhiteSpace(invoice.BuyerReference))
            diagnostics.Add(Warning("PXAEINV117", "XRechnung commonly requires a buyer reference (BT-10)."));
    }

    private static void ValidateParty(
        PvadEInvoiceBusinessParty? party,
        string role,
        string code,
        ICollection<PvadEInvoiceDiagnostic> diagnostics)
    {
        if (party is null || string.IsNullOrWhiteSpace(party.Name) || party.Name.Trim().Length > 200)
        {
            diagnostics.Add(Error(code, $"The {role} name is required and cannot exceed 200 characters."));
            return;
        }
        if (party.Address is { } address)
        {
            Required(address.Street, 200, code, $"The {role} street is required.", diagnostics);
            Required(address.City, 200, code, $"The {role} city is required.", diagnostics);
            Required(address.PostalCode, 32, code, $"The {role} postal code is required.", diagnostics);
            Required(address.CountryCode, 2, code, $"The {role} country code is required.", diagnostics);
            if (!string.IsNullOrWhiteSpace(address.CountryCode) &&
                (address.CountryCode.Trim().Length != 2 || !address.CountryCode.Trim().All(char.IsLetter)))
                diagnostics.Add(Error(code, $"The {role} country code must contain two letters."));
        }
    }

    private static void Required(
        string? value,
        int maximumLength,
        string code,
        string message,
        ICollection<PvadEInvoiceDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
            diagnostics.Add(Error(code, message));
    }

    private static InvoiceCalculations Calculate(PvadEInvoiceDocument invoice)
    {
        var lines = invoice.Lines.Select(line => new CalculatedLine(
            line,
            Round(line.Quantity * line.UnitPrice, invoice.CurrencyDecimalPlaces))).ToArray();
        var taxes = lines
            .GroupBy(static line => (line.Source.TaxCategoryCode, line.Source.TaxRate))
            .Select(group =>
            {
                var basis = group.Sum(static line => line.NetAmount);
                return new CalculatedTax(
                    group.Key.TaxCategoryCode,
                    group.Key.TaxRate,
                    basis,
                    Round(basis * group.Key.TaxRate / 100m, invoice.CurrencyDecimalPlaces));
            })
            .OrderBy(static tax => tax.Rate)
            .ThenBy(static tax => tax.CategoryCode, StringComparer.Ordinal)
            .ToArray();
        var lineTotal = lines.Sum(static line => line.NetAmount);
        var taxTotal = taxes.Sum(static tax => tax.Amount);
        return new(lines, taxes, lineTotal, taxTotal, lineTotal + taxTotal);
    }

    private static XDocument CreateCiiInvoice(
        PvadEInvoiceDocument invoice,
        PvadEInvoiceOptions options,
        InvoiceCalculations calculations)
    {
        var currency = invoice.Currency.Trim().ToUpperInvariant();
        var transaction = new XElement(CiiRoot + "SupplyChainTradeTransaction");
        foreach (var line in calculations.Lines)
        {
            transaction.Add(new XElement(CiiAggregate + "IncludedSupplyChainTradeLineItem",
                new XElement(CiiAggregate + "AssociatedDocumentLineDocument",
                    new XElement(CiiAggregate + "LineID", line.Source.Identifier.Trim())),
                new XElement(CiiAggregate + "SpecifiedTradeProduct",
                    new XElement(CiiAggregate + "Name", line.Source.Description.Trim())),
                new XElement(CiiAggregate + "SpecifiedLineTradeAgreement",
                    new XElement(CiiAggregate + "NetPriceProductTradePrice",
                        CiiAmount("ChargeAmount", line.Source.UnitPrice, invoice.CurrencyDecimalPlaces))),
                new XElement(CiiAggregate + "SpecifiedLineTradeDelivery",
                    new XElement(CiiAggregate + "BilledQuantity",
                        new XAttribute("unitCode", line.Source.UnitCode.Trim().ToUpperInvariant()),
                        Decimal(line.Source.Quantity))),
                new XElement(CiiAggregate + "SpecifiedLineTradeSettlement",
                    CiiTax(line.Source.TaxCategoryCode, line.Source.TaxRate),
                    new XElement(CiiAggregate + "SpecifiedTradeSettlementLineMonetarySummation",
                        CiiAmount("LineTotalAmount", line.NetAmount, invoice.CurrencyDecimalPlaces)))));
        }

        transaction.Add(
            new XElement(CiiAggregate + "ApplicableHeaderTradeAgreement",
                Optional(CiiAggregate + "BuyerReference", invoice.BuyerReference),
                CiiParty("SellerTradeParty", invoice.Seller),
                CiiParty("BuyerTradeParty", invoice.Buyer)),
            new XElement(CiiAggregate + "ApplicableHeaderTradeDelivery"),
            new XElement(CiiAggregate + "ApplicableHeaderTradeSettlement",
                Optional(CiiAggregate + "PaymentReference", invoice.PaymentReference),
                new XElement(CiiAggregate + "InvoiceCurrencyCode", currency),
                calculations.Taxes.Select(tax => new XElement(CiiAggregate + "ApplicableTradeTax",
                    CiiAmount("CalculatedAmount", tax.Amount, invoice.CurrencyDecimalPlaces),
                    new XElement(CiiAggregate + "TypeCode", "VAT"),
                    CiiAmount("BasisAmount", tax.Basis, invoice.CurrencyDecimalPlaces),
                    new XElement(CiiAggregate + "CategoryCode", tax.CategoryCode.Trim().ToUpperInvariant()),
                    new XElement(CiiAggregate + "RateApplicablePercent", Decimal(tax.Rate)))),
                invoice.DueDate is null ? null : new XElement(CiiAggregate + "SpecifiedTradePaymentTerms",
                    new XElement(CiiAggregate + "DueDateDateTime",
                        new XElement(CiiUnqualified + "DateTimeString",
                            new XAttribute("format", "102"), Date(invoice.DueDate.Value)))),
                new XElement(CiiAggregate + "SpecifiedTradeSettlementHeaderMonetarySummation",
                    CiiAmount("LineTotalAmount", calculations.LineTotal, invoice.CurrencyDecimalPlaces),
                    CiiAmount("ChargeTotalAmount", 0, invoice.CurrencyDecimalPlaces),
                    CiiAmount("AllowanceTotalAmount", 0, invoice.CurrencyDecimalPlaces),
                    CiiAmount("TaxBasisTotalAmount", calculations.LineTotal, invoice.CurrencyDecimalPlaces),
                    AmountElement(CiiAggregate + "TaxTotalAmount", calculations.TaxTotal, currency, invoice.CurrencyDecimalPlaces),
                    CiiAmount("GrandTotalAmount", calculations.GrandTotal, invoice.CurrencyDecimalPlaces),
                    CiiAmount("DuePayableAmount", calculations.GrandTotal, invoice.CurrencyDecimalPlaces))));

        return new XDocument(new XElement(CiiRoot + "CrossIndustryInvoice",
            new XAttribute(XNamespace.Xmlns + "rsm", CiiRoot),
            new XAttribute(XNamespace.Xmlns + "ram", CiiAggregate),
            new XAttribute(XNamespace.Xmlns + "udt", CiiUnqualified),
            new XElement(CiiRoot + "ExchangedDocumentContext",
                new XElement(CiiAggregate + "GuidelineSpecifiedDocumentContextParameter",
                    new XElement(CiiAggregate + "ID", CiiGuideline(options.Profile)))),
            new XElement(CiiRoot + "ExchangedDocument",
                new XElement(CiiAggregate + "ID", invoice.InvoiceNumber.Trim()),
                new XElement(CiiAggregate + "TypeCode", "380"),
                new XElement(CiiAggregate + "IssueDateTime",
                    new XElement(CiiUnqualified + "DateTimeString",
                        new XAttribute("format", "102"), Date(invoice.IssueDate)))),
            transaction));
    }

    private static XDocument CreateUblInvoice(
        PvadEInvoiceDocument invoice,
        InvoiceCalculations calculations)
    {
        var currency = invoice.Currency.Trim().ToUpperInvariant();
        return new XDocument(new XElement(UblInvoice + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cbc", UblBasic),
            new XAttribute(XNamespace.Xmlns + "cac", UblAggregate),
            new XElement(UblBasic + "CustomizationID", XRechnungCustomizationId),
            new XElement(UblBasic + "ID", invoice.InvoiceNumber.Trim()),
            new XElement(UblBasic + "IssueDate", invoice.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            invoice.DueDate is null ? null : new XElement(UblBasic + "DueDate", invoice.DueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(UblBasic + "InvoiceTypeCode", "380"),
            new XElement(UblBasic + "DocumentCurrencyCode", currency),
            Optional(UblBasic + "BuyerReference", invoice.BuyerReference),
            new XElement(UblAggregate + "AccountingSupplierParty", UblParty(invoice.Seller)),
            new XElement(UblAggregate + "AccountingCustomerParty", UblParty(invoice.Buyer)),
            UblTaxTotal(calculations, currency, invoice.CurrencyDecimalPlaces),
            new XElement(UblAggregate + "LegalMonetaryTotal",
                UblAmount("LineExtensionAmount", calculations.LineTotal, currency, invoice.CurrencyDecimalPlaces),
                UblAmount("TaxExclusiveAmount", calculations.LineTotal, currency, invoice.CurrencyDecimalPlaces),
                UblAmount("TaxInclusiveAmount", calculations.GrandTotal, currency, invoice.CurrencyDecimalPlaces),
                UblAmount("PayableAmount", calculations.GrandTotal, currency, invoice.CurrencyDecimalPlaces)),
            calculations.Lines.Select(line =>
                UblLine(line, currency, invoice.CurrencyDecimalPlaces))));
    }

    private static XElement UblTaxTotal(
        InvoiceCalculations calculations,
        string currency,
        int decimalPlaces) =>
        new(UblAggregate + "TaxTotal",
            UblAmount("TaxAmount", calculations.TaxTotal, currency, decimalPlaces),
            calculations.Taxes.Select(tax => new XElement(UblAggregate + "TaxSubtotal",
                UblAmount("TaxableAmount", tax.Basis, currency, decimalPlaces),
                UblAmount("TaxAmount", tax.Amount, currency, decimalPlaces),
                new XElement(UblAggregate + "TaxCategory",
                    new XElement(UblBasic + "ID", tax.CategoryCode.Trim().ToUpperInvariant()),
                    new XElement(UblBasic + "Percent", Decimal(tax.Rate)),
                    new XElement(UblAggregate + "TaxScheme",
                        new XElement(UblBasic + "ID", "VAT"))))));

    private static XElement UblLine(CalculatedLine line, string currency, int decimalPlaces) =>
        new(UblAggregate + "InvoiceLine",
            new XElement(UblBasic + "ID", line.Source.Identifier.Trim()),
            new XElement(UblBasic + "InvoicedQuantity",
                new XAttribute("unitCode", line.Source.UnitCode.Trim().ToUpperInvariant()),
                Decimal(line.Source.Quantity)),
            UblAmount("LineExtensionAmount", line.NetAmount, currency, decimalPlaces),
            new XElement(UblAggregate + "Item",
                new XElement(UblBasic + "Name", line.Source.Description.Trim()),
                new XElement(UblAggregate + "ClassifiedTaxCategory",
                    new XElement(UblBasic + "ID", line.Source.TaxCategoryCode.Trim().ToUpperInvariant()),
                    new XElement(UblBasic + "Percent", Decimal(line.Source.TaxRate)),
                    new XElement(UblAggregate + "TaxScheme",
                        new XElement(UblBasic + "ID", "VAT")))),
            new XElement(UblAggregate + "Price",
                UblAmount("PriceAmount", line.Source.UnitPrice, currency, decimalPlaces)));

    private const string XRechnungCustomizationId =
        "urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0";

    private static XElement CiiParty(string elementName, PvadEInvoiceBusinessParty party) =>
        new(CiiAggregate + elementName,
            Optional(CiiAggregate + "ID", party.Identifier),
            new XElement(CiiAggregate + "Name", party.Name.Trim()),
            party.Address is null ? null : new XElement(CiiAggregate + "PostalTradeAddress",
                new XElement(CiiAggregate + "PostcodeCode", party.Address.PostalCode.Trim()),
                Optional(CiiAggregate + "LineOne", party.Address.Street),
                Optional(CiiAggregate + "LineTwo", party.Address.AdditionalLine),
                new XElement(CiiAggregate + "CityName", party.Address.City.Trim()),
                new XElement(CiiAggregate + "CountryID", party.Address.CountryCode.Trim().ToUpperInvariant())),
            string.IsNullOrWhiteSpace(party.TaxIdentifier) ? null : new XElement(CiiAggregate + "SpecifiedTaxRegistration",
                new XElement(CiiAggregate + "ID", new XAttribute("schemeID", "VA"), party.TaxIdentifier.Trim())));

    private static XElement UblParty(PvadEInvoiceBusinessParty party) =>
        new(UblAggregate + "Party",
            Optional(UblBasic + "EndpointID", party.EndpointIdentifier),
            string.IsNullOrWhiteSpace(party.Identifier) ? null : new XElement(UblAggregate + "PartyIdentification",
                new XElement(UblBasic + "ID", party.Identifier.Trim())),
            new XElement(UblAggregate + "PartyName", new XElement(UblBasic + "Name", party.Name.Trim())),
            party.Address is null ? null : new XElement(UblAggregate + "PostalAddress",
                new XElement(UblBasic + "StreetName", party.Address.Street.Trim()),
                Optional(UblBasic + "AdditionalStreetName", party.Address.AdditionalLine),
                new XElement(UblBasic + "CityName", party.Address.City.Trim()),
                new XElement(UblBasic + "PostalZone", party.Address.PostalCode.Trim()),
                new XElement(UblAggregate + "Country", new XElement(UblBasic + "IdentificationCode", party.Address.CountryCode.Trim().ToUpperInvariant()))),
            string.IsNullOrWhiteSpace(party.TaxIdentifier) ? null : new XElement(UblAggregate + "PartyTaxScheme",
                new XElement(UblBasic + "CompanyID", party.TaxIdentifier.Trim()),
                new XElement(UblAggregate + "TaxScheme", new XElement(UblBasic + "ID", "VAT"))),
            new XElement(UblAggregate + "PartyLegalEntity",
                new XElement(UblBasic + "RegistrationName", party.Name.Trim())));

    private static XElement CiiTax(string categoryCode, decimal taxRate) =>
        new(CiiAggregate + "ApplicableTradeTax",
            new XElement(CiiAggregate + "TypeCode", "VAT"),
            new XElement(CiiAggregate + "CategoryCode", categoryCode.Trim().ToUpperInvariant()),
            new XElement(CiiAggregate + "RateApplicablePercent", Decimal(taxRate)));

    private static XElement AmountElement(
        XName name,
        decimal value,
        string currency,
        int decimalPlaces) =>
        new(name, new XAttribute("currencyID", currency), Money(value, decimalPlaces));

    private static XElement CiiAmount(string name, decimal value, int decimalPlaces) =>
        new(CiiAggregate + name, Money(value, decimalPlaces));

    private static XElement UblAmount(
        string name,
        decimal value,
        string currency,
        int decimalPlaces) =>
        new(UblBasic + name, new XAttribute("currencyID", currency), Money(value, decimalPlaces));

    private static XElement? Optional(XName name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new XElement(name, value.Trim());

    private static string CiiGuideline(string profile) => NormalizeProfile(profile) switch
    {
        "MINIMUM" => "urn:factur-x.eu:1p0:minimum",
        "BASIC WL" => "urn:factur-x.eu:1p0:basicwl",
        "BASIC" => "urn:factur-x.eu:1p0:basic",
        "EXTENDED" => "urn:factur-x.eu:1p0:extended",
        "EXTENDED-CTC-FR" => "urn:factur-x.eu:1p0:extended-ctc-fr",
        "XRECHNUNG" => "urn:factur-x.eu:1p0:xrechnung",
        _ => "urn:factur-x.eu:1p0:en16931"
    };

    private static decimal Round(decimal value, int decimalPlaces) =>
        Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);

    private static string Money(decimal value, int decimalPlaces) =>
        Round(value, decimalPlaces).ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);

    private static string Decimal(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string Date(DateOnly date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private sealed record CalculatedLine(PvadEInvoiceDocumentLine Source, decimal NetAmount);
    private sealed record CalculatedTax(string CategoryCode, decimal Rate, decimal Basis, decimal Amount);
    private sealed record InvoiceCalculations(
        IReadOnlyList<CalculatedLine> Lines,
        IReadOnlyList<CalculatedTax> Taxes,
        decimal LineTotal,
        decimal TaxTotal,
        decimal GrandTotal);
}
