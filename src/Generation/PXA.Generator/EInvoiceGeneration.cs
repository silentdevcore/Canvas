using System.Globalization;
using System.Text;
using System.Xml.Linq;
using PXA.Pdf;

namespace PXA.Generator;

public sealed record PxaEInvoiceAddress(
    string Street,
    string City,
    string PostalCode,
    string CountryCode,
    string? AdditionalLine = null);

public sealed record PxaEInvoiceBusinessParty(
    string Name,
    string? Identifier = null,
    string? TaxIdentifier = null,
    string? EndpointIdentifier = null,
    PxaEInvoiceAddress? Address = null);

public sealed record PxaEInvoiceDocumentLine(
    string Identifier,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    decimal TaxRate,
    string TaxCategoryCode = "S");

public sealed record PxaEInvoiceDocument
{
    public required string InvoiceNumber { get; init; }
    public required DateOnly IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public required string Currency { get; init; }
    public required PxaEInvoiceBusinessParty Seller { get; init; }
    public required PxaEInvoiceBusinessParty Buyer { get; init; }
    public required IReadOnlyList<PxaEInvoiceDocumentLine> Lines { get; init; }
    public string? BuyerReference { get; init; }
    public string? PaymentReference { get; init; }
    public int CurrencyDecimalPlaces { get; init; } = 2;
}

public sealed record PxaEInvoiceGenerationResult(
    bool IsGenerated,
    ReadOnlyMemory<byte> Xml,
    PxaEInvoiceSemanticData? SemanticData,
    IReadOnlyList<PxaEInvoiceDiagnostic> Diagnostics);

public sealed record PxaEInvoicePdfGenerationResult(
    bool IsGenerated,
    ReadOnlyMemory<byte> Pdf,
    ReadOnlyMemory<byte> Xml,
    PxaEInvoiceSemanticData? SemanticData,
    IReadOnlyList<PxaEInvoiceDiagnostic> Diagnostics);

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

    public static PxaEInvoiceGenerationResult GenerateXml(
        PxaEInvoiceDocument invoice,
        PxaEInvoiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(options);
        var diagnostics = new List<PxaEInvoiceDiagnostic>();
        ValidateOptions(options, diagnostics);
        ValidateDocument(invoice, options.Standard, diagnostics);
        if (diagnostics.Any(static diagnostic => diagnostic.Severity == "error"))
            return new(false, ReadOnlyMemory<byte>.Empty, null, diagnostics);

        var calculations = Calculate(invoice);
        var document = options.Standard == PxaEInvoiceStandard.XRechnung
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

    public static PxaEInvoicePdfGenerationResult CreatePdf(
        PxaEInvoiceDocument invoice,
        PxaEInvoiceOptions options,
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
            var document = Pdf.CreateDocument();
            document.FontLoader = fontLoader;
            document.Language = "en";
            document.Info.Title = $"Invoice {invoice.InvoiceNumber.Trim()}";
            document.Info.Subject = $"{options.Standard} electronic invoice";
            RenderInvoice(document, invoice, calculations);
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

    public static string GetDefaultFileName(PxaEInvoiceStandard standard) => DefaultFileName(standard);

    private static void RenderInvoice(
        PdfDocument document,
        PxaEInvoiceDocument invoice,
        InvoiceCalculations calculations)
    {
        const double left = 42;
        const double right = 553;
        const double contentWidth = right - left;
        const double rowHeight = 30;
        var pageNumber = 0;
        var page = NewInvoicePage(document, invoice, ref pageNumber, showParties: true);
        var top = 250d;
        DrawTableHeader(page, left, top, contentWidth);
        top += 26;

        foreach (var line in calculations.Lines)
        {
            if (top + rowHeight > 740)
            {
                page = NewInvoicePage(document, invoice, ref pageNumber, showParties: false);
                top = 82;
                DrawTableHeader(page, left, top, contentWidth);
                top += 26;
            }
            DrawInvoiceLine(page, line, invoice.Currency, invoice.CurrencyDecimalPlaces, left, top, contentWidth, rowHeight);
            top += rowHeight;
        }

        if (top + 150 > 790)
        {
            page = NewInvoicePage(document, invoice, ref pageNumber, showParties: false);
            top = 82;
        }
        DrawTotals(page, calculations, invoice.Currency, invoice.CurrencyDecimalPlaces, top + 18, right);
    }

    private static PdfPage NewInvoicePage(
        PdfDocument document,
        PxaEInvoiceDocument invoice,
        ref int pageNumber,
        bool showParties)
    {
        pageNumber++;
        var page = document.AddPage(PdfPageSizes.A4Width, PdfPageSizes.A4Height);
        page.DrawTextFromTop("INVOICE", 42, 44, Text(22, bold: true));
        page.DrawTextFromTop(invoice.InvoiceNumber.Trim(), 390, 46, Text(12, bold: true));
        page.DrawTextFromTop($"Page {pageNumber}", 486, 68, Text(8));
        page.DrawLineFromTop(42, 78, 553, 78, 1, PdfColor.FromRgb(34, 49, 63));
        if (!showParties) return page;

        page.DrawTextFromTop("Seller", 42, 102, Text(9, bold: true));
        DrawParty(page, invoice.Seller, 42, 120);
        page.DrawTextFromTop("Buyer", 310, 102, Text(9, bold: true));
        DrawParty(page, invoice.Buyer, 310, 120);
        page.DrawTextFromTop($"Issue date: {invoice.IssueDate:yyyy-MM-dd}", 42, 208, Text(9));
        if (invoice.DueDate is { } dueDate)
            page.DrawTextFromTop($"Due date: {dueDate:yyyy-MM-dd}", 210, 208, Text(9));
        page.DrawTextFromTop($"Currency: {invoice.Currency.Trim().ToUpperInvariant()}", 410, 208, Text(9));
        return page;
    }

    private static void DrawParty(PdfPage page, PxaEInvoiceBusinessParty party, double x, double top)
    {
        var lines = new List<string> { party.Name.Trim() };
        if (party.Address is { } address)
        {
            lines.Add(address.Street.Trim());
            if (!string.IsNullOrWhiteSpace(address.AdditionalLine)) lines.Add(address.AdditionalLine.Trim());
            lines.Add($"{address.PostalCode.Trim()} {address.City.Trim()}");
            lines.Add(address.CountryCode.Trim().ToUpperInvariant());
        }
        page.DrawTextBoxFromTop(string.Join('\n', lines), x, top, 230, 75, new PdfTextBoxOptions
        {
            FontSize = 9,
            LineHeight = 13,
            Language = "en"
        });
    }

    private static void DrawTableHeader(PdfPage page, double left, double top, double width)
    {
        page.DrawRectangleFromTop(left, top, width, 24, fill: true,
            strokeColor: PdfColor.FromRgb(34, 49, 63), fillColor: PdfColor.FromRgb(34, 49, 63));
        page.DrawTextFromTop("Item", left + 8, top + 7, Text(8, bold: true, color: PdfColor.White));
        page.DrawTextFromTop("Description", left + 48, top + 7, Text(8, bold: true, color: PdfColor.White));
        page.DrawTextFromTop("Qty", left + 298, top + 7, Text(8, bold: true, color: PdfColor.White));
        page.DrawTextFromTop("Unit price", left + 350, top + 7, Text(8, bold: true, color: PdfColor.White));
        page.DrawTextFromTop("Tax", left + 418, top + 7, Text(8, bold: true, color: PdfColor.White));
        page.DrawTextFromTop("Net", left + 466, top + 7, Text(8, bold: true, color: PdfColor.White));
    }

    private static void DrawInvoiceLine(
        PdfPage page,
        CalculatedLine line,
        string currency,
        int decimalPlaces,
        double left,
        double top,
        double width,
        double height)
    {
        page.DrawRectangleFromTop(left, top, width, height, lineWidth: 0.5,
            strokeColor: PdfColor.FromRgb(190, 198, 204));
        page.DrawTextFromTop(line.Source.Identifier.Trim(), left + 8, top + 10, Text(8));
        page.DrawTextBoxFromTop(line.Source.Description.Trim(), left + 48, top + 6, 240, height - 8,
            new PdfTextBoxOptions { FontSize = 8, LineHeight = 10, Language = "en" });
        page.DrawTextFromTop(Decimal(line.Source.Quantity), left + 298, top + 10, Text(8));
        page.DrawTextFromTop(Money(line.Source.UnitPrice, decimalPlaces), left + 350, top + 10, Text(8));
        page.DrawTextFromTop($"{Decimal(line.Source.TaxRate)}%", left + 418, top + 10, Text(8));
        page.DrawTextFromTop($"{Money(line.NetAmount, decimalPlaces)} {currency.Trim().ToUpperInvariant()}", left + 466, top + 10, Text(8));
    }

    private static void DrawTotals(
        PdfPage page,
        InvoiceCalculations calculations,
        string currency,
        int decimalPlaces,
        double top,
        double right)
    {
        var code = currency.Trim().ToUpperInvariant();
        var x = right - 220;
        page.DrawTextFromTop("Net total", x, top, Text(9));
        page.DrawTextFromTop($"{Money(calculations.LineTotal, decimalPlaces)} {code}", right - 95, top, Text(9));
        top += 20;
        foreach (var tax in calculations.Taxes)
        {
            page.DrawTextFromTop($"Tax {Decimal(tax.Rate)}%", x, top, Text(9));
            page.DrawTextFromTop($"{Money(tax.Amount, decimalPlaces)} {code}", right - 95, top, Text(9));
            top += 20;
        }

        // With one tax group the group row already is the tax total. With several, the aggregate
        // would otherwise appear nowhere on the page, so a reader - and the PDF/XML consistency
        // comparison - could not confirm the tax total the embedded XML declares.
        if (calculations.Taxes.Count > 1)
        {
            page.DrawTextFromTop("Tax total", x, top, Text(9));
            page.DrawTextFromTop($"{Money(calculations.TaxTotal, decimalPlaces)} {code}", right - 95, top, Text(9));
            top += 20;
        }

        page.DrawLineFromTop(x, top, right, top, 1, PdfColor.FromRgb(34, 49, 63));
        page.DrawTextFromTop("Amount payable", x, top + 12, Text(11, bold: true));
        page.DrawTextFromTop($"{Money(calculations.GrandTotal, decimalPlaces)} {code}", right - 110, top + 12, Text(11, bold: true));
    }

    private static PdfDrawTextOptions Text(double size, bool bold = false, IPdfColor? color = null) => new()
    {
        FontSize = size,
        Bold = bold,
        FillColor = color ?? PdfColor.Black,
        Language = "en"
    };

    private static void ValidateDocument(
        PxaEInvoiceDocument invoice,
        PxaEInvoiceStandard standard,
        ICollection<PxaEInvoiceDiagnostic> diagnostics)
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
        if (standard == PxaEInvoiceStandard.XRechnung && string.IsNullOrWhiteSpace(invoice.BuyerReference))
            diagnostics.Add(Warning("PXAEINV117", "XRechnung commonly requires a buyer reference (BT-10)."));
    }

    private static void ValidateParty(
        PxaEInvoiceBusinessParty? party,
        string role,
        string code,
        ICollection<PxaEInvoiceDiagnostic> diagnostics)
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
        ICollection<PxaEInvoiceDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
            diagnostics.Add(Error(code, message));
    }

    private static InvoiceCalculations Calculate(PxaEInvoiceDocument invoice)
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
        PxaEInvoiceDocument invoice,
        PxaEInvoiceOptions options,
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
        PxaEInvoiceDocument invoice,
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

    private static XElement CiiParty(string elementName, PxaEInvoiceBusinessParty party) =>
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

    private static XElement UblParty(PxaEInvoiceBusinessParty party) =>
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

    private sealed record CalculatedLine(PxaEInvoiceDocumentLine Source, decimal NetAmount);
    private sealed record CalculatedTax(string CategoryCode, decimal Rate, decimal Basis, decimal Amount);
    private sealed record InvoiceCalculations(
        IReadOnlyList<CalculatedLine> Lines,
        IReadOnlyList<CalculatedTax> Taxes,
        decimal LineTotal,
        decimal TaxTotal,
        decimal GrandTotal);
}
