using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using PXA.Pdf;

namespace PXA.Generator;

public enum PxaEInvoiceStandard
{
    FacturX,
    Zugferd,
    XRechnung
}

public sealed record PxaEInvoiceOptions
{
    public required PxaEInvoiceStandard Standard { get; init; }
    public required string Version { get; init; }
    public required string Profile { get; init; }
    public string? FileName { get; init; }
}

public sealed record PxaEInvoiceDiagnostic(string Code, string Severity, string Message);

public sealed record PxaEInvoiceParty(string? Name, string? Identifier);

public sealed record PxaEInvoiceLine(
    string? Identifier,
    string? Description,
    decimal? Quantity,
    string? UnitCode,
    decimal? NetAmount);

public sealed record PxaEInvoiceSemanticData(
    string? InvoiceNumber,
    string? IssueDate,
    string? Currency,
    PxaEInvoiceParty Seller,
    PxaEInvoiceParty Buyer,
    decimal? LineNetTotal,
    decimal? TaxExclusiveTotal,
    decimal? TaxTotal,
    decimal? TaxInclusiveTotal,
    decimal? PayableAmount,
    IReadOnlyList<PxaEInvoiceLine> Lines);

public sealed record PxaEInvoiceRecognitionReport(
    bool IsRecognized,
    string? Syntax,
    string? GuidelineIdentifier,
    string? Profile,
    IReadOnlyList<PxaEInvoiceDiagnostic> Diagnostics,
    PxaEInvoiceSemanticData? SemanticData = null);

/// <summary>How invoice XML appears as an associated file inside a hybrid PDF.</summary>
public sealed record PxaEInvoiceAttachmentDescriptor(
    string FileName,
    string MimeType,
    PdfAssociatedFileRelationship Relationship,
    string Description,
    PdfAssociatedFileXmpMetadata XmpMetadata);

public sealed record PxaEInvoiceValidationReport(
    bool IsStructurallyValid,
    bool IsConformanceValidated,
    string? Syntax,
    string? GuidelineIdentifier,
    IReadOnlyList<PxaEInvoiceDiagnostic> Diagnostics,
    string ConformanceStatus = "notEvaluated",
    string? ValidatorArtifactSetId = null,
    string? ValidatorArtifactSetSha256 = null);

/// <summary>
/// Adds structurally validated electronic-invoice XML to a PDF document. Full XSD and
/// Schematron conformance is a separate, versioned validation step.
/// </summary>
public static partial class EInvoice
{
    public const int MaximumXmlBytes = 10 * 1024 * 1024;
    public const string CurrentFacturXVersion = "1.09.2";
    public const string CurrentZugferdVersion = "2.5.2";
    public const string CurrentXRechnungVersion = "3.0.2";
    private static readonly HashSet<string> HybridProfiles = new(StringComparer.Ordinal)
    {
        "MINIMUM", "BASIC WL", "BASIC", "EN 16931", "EXTENDED", "EXTENDED-CTC-FR", "XRECHNUNG"
    };

    public static PxaEInvoiceValidationReport Validate(
        ReadOnlyMemory<byte> xml,
        PxaEInvoiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var diagnostics = new List<PxaEInvoiceDiagnostic>();
        ValidateOptions(options, diagnostics);
        if (diagnostics.Any(static item => item.Severity == "error"))
            return Report(false, null, null, diagnostics);

        var recognition = Recognize(xml);
        diagnostics.AddRange(recognition.Diagnostics);
        if (!recognition.IsRecognized)
            return Report(false, recognition.Syntax, recognition.GuidelineIdentifier, diagnostics);

        ValidateStandardMatch(options.Standard, recognition.Syntax!, recognition.GuidelineIdentifier, diagnostics);

        var normalizedProfile = NormalizeProfile(options.Profile);
        if (recognition.Profile is not null && normalizedProfile is not null &&
            !string.Equals(recognition.Profile, normalizedProfile, StringComparison.Ordinal))
        {
            diagnostics.Add(Error(
                "PXAEINV015",
                $"The declared profile '{normalizedProfile}' does not match XML profile '{recognition.Profile}'."));
        }

        if (diagnostics.All(static item => item.Severity != "error"))
        {
            diagnostics.Add(new PxaEInvoiceDiagnostic(
                "PXAEINV000",
                "information",
                "The XML passed secure structural recognition. XSD and Schematron conformance have not been evaluated."));
        }

        return Report(
            diagnostics.All(static item => item.Severity != "error"),
            recognition.Syntax,
            recognition.GuidelineIdentifier,
            diagnostics);
    }

    public static PxaEInvoiceRecognitionReport Recognize(ReadOnlyMemory<byte> xml)
    {
        var diagnostics = new List<PxaEInvoiceDiagnostic>();
        if (xml.IsEmpty)
            diagnostics.Add(Error("PXAEINV001", "Invoice XML cannot be empty."));
        else if (xml.Length > MaximumXmlBytes)
            diagnostics.Add(Error("PXAEINV002", "Invoice XML cannot exceed " + MaximumXmlBytes + " bytes."));
        if (diagnostics.Count > 0)
            return new(false, null, null, null, diagnostics);

        XDocument document;
        try
        {
            using var stream = new MemoryStream(xml.ToArray(), writable: false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumXmlBytes,
                MaxCharactersFromEntities = 0,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            });
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException)
        {
            diagnostics.Add(Error("PXAEINV003", "Invoice XML is malformed or contains prohibited XML features."));
            return new(false, null, null, null, diagnostics);
        }

        var root = document.Root;
        if (root is null)
        {
            diagnostics.Add(Error("PXAEINV004", "Invoice XML has no document element."));
            return new(false, null, null, null, diagnostics);
        }

        var syntax = DetectSyntax(root);
        if (syntax is null)
        {
            diagnostics.Add(Error(
                "PXAEINV005",
                "Only UN/CEFACT CrossIndustryInvoice and UBL Invoice/CreditNote syntaxes are supported."));
            return new(false, null, null, null, diagnostics);
        }

        var guideline = FindGuidelineIdentifier(document, syntax);
        if (string.IsNullOrWhiteSpace(guideline))
        {
            diagnostics.Add(Error(
                "PXAEINV006",
                "The invoice does not declare a guideline or customization identifier."));
        }
        var semanticData = ExtractSemanticData(document, syntax);
        AddSemanticDiagnostics(semanticData, diagnostics);
        return new(
            diagnostics.All(static item => item.Severity != "error"),
            syntax,
            guideline,
            DetectProfile(guideline),
            diagnostics,
            semanticData);
    }

    private static PxaEInvoiceSemanticData ExtractSemanticData(XDocument document, string syntax) =>
        syntax == "CII" ? ExtractCiiSemanticData(document) : ExtractUblSemanticData(document);

    private static PxaEInvoiceSemanticData ExtractCiiSemanticData(XDocument document)
    {
        var exchangedDocument = First(document.Root, "ExchangedDocument");
        var transaction = First(document.Root, "SupplyChainTradeTransaction");
        var agreement = First(transaction, "ApplicableHeaderTradeAgreement");
        var settlement = First(transaction, "ApplicableHeaderTradeSettlement");
        var summation = First(settlement, "SpecifiedTradeSettlementHeaderMonetarySummation");
        var issueDate = First(exchangedDocument, "IssueDateTime");
        var seller = First(agreement, "SellerTradeParty");
        var buyer = First(agreement, "BuyerTradeParty");

        var lines = Children(transaction, "IncludedSupplyChainTradeLineItem")
            .Take(10_000)
            .Select(line =>
            {
                var documentLine = First(line, "AssociatedDocumentLineDocument");
                var product = First(line, "SpecifiedTradeProduct");
                var delivery = First(line, "SpecifiedLineTradeDelivery");
                var lineSettlement = First(line, "SpecifiedLineTradeSettlement");
                var lineSummation = First(lineSettlement, "SpecifiedTradeSettlementLineMonetarySummation");
                var quantity = First(delivery, "BilledQuantity");
                return new PxaEInvoiceLine(
                    Value(First(documentLine, "LineID")),
                    Value(First(product, "Name")),
                    DecimalValue(quantity),
                    AttributeValue(quantity, "unitCode"),
                    DecimalValue(First(lineSummation, "LineTotalAmount")));
            })
            .ToArray();

        return new PxaEInvoiceSemanticData(
            Value(First(exchangedDocument, "ID")),
            Value(First(issueDate, "DateTimeString")),
            Value(First(settlement, "InvoiceCurrencyCode")),
            Party(seller),
            Party(buyer),
            DecimalValue(First(summation, "LineTotalAmount")),
            DecimalValue(First(summation, "TaxBasisTotalAmount")),
            DecimalValue(First(summation, "TaxTotalAmount")) ??
                DecimalValue(First(settlement, "TaxTotalAmount")),
            DecimalValue(First(summation, "GrandTotalAmount")),
            DecimalValue(First(summation, "DuePayableAmount")),
            lines);
    }

    private static PxaEInvoiceSemanticData ExtractUblSemanticData(XDocument document)
    {
        var root = document.Root!;
        var supplier = First(First(root, "AccountingSupplierParty"), "Party");
        var customer = First(First(root, "AccountingCustomerParty"), "Party");
        var monetary = First(root, "LegalMonetaryTotal");
        var quantityName = root.Name.LocalName == "CreditNote" ? "CreditedQuantity" : "InvoicedQuantity";
        var lineName = root.Name.LocalName == "CreditNote" ? "CreditNoteLine" : "InvoiceLine";
        var lines = Children(root, lineName)
            .Take(10_000)
            .Select(line =>
            {
                var quantity = First(line, quantityName);
                return new PxaEInvoiceLine(
                    Value(First(line, "ID")),
                    Value(First(First(line, "Item"), "Name")),
                    DecimalValue(quantity),
                    AttributeValue(quantity, "unitCode"),
                    DecimalValue(First(line, "LineExtensionAmount")));
            })
            .ToArray();

        return new PxaEInvoiceSemanticData(
            Value(First(root, "ID")),
            Value(First(root, "IssueDate")),
            Value(First(root, "DocumentCurrencyCode")),
            UblParty(supplier),
            UblParty(customer),
            DecimalValue(First(monetary, "LineExtensionAmount")),
            DecimalValue(First(monetary, "TaxExclusiveAmount")),
            DecimalValue(First(First(root, "TaxTotal"), "TaxAmount")),
            DecimalValue(First(monetary, "TaxInclusiveAmount")),
            DecimalValue(First(monetary, "PayableAmount")),
            lines);
    }

    private static PxaEInvoiceParty Party(XElement? party) => new(
        Value(First(party, "Name")),
        Value(First(party, "GlobalID")) ?? Value(First(party, "ID")));

    private static PxaEInvoiceParty UblParty(XElement? party)
    {
        var name = Value(First(First(party, "PartyName"), "Name")) ??
            Value(First(First(party, "PartyLegalEntity"), "RegistrationName"));
        var identifier = Value(First(party, "EndpointID")) ??
            Value(First(First(party, "PartyIdentification"), "ID")) ??
            Value(First(First(party, "PartyLegalEntity"), "CompanyID"));
        return new(name, identifier);
    }

    private static void AddSemanticDiagnostics(
        PxaEInvoiceSemanticData data,
        ICollection<PxaEInvoiceDiagnostic> diagnostics)
    {
        AddMissing(data.InvoiceNumber, "PXAEINV030", "The invoice number could not be extracted.", diagnostics);
        AddMissing(data.Currency, "PXAEINV031", "The invoice currency could not be extracted.", diagnostics);
        AddMissing(data.Seller.Name, "PXAEINV032", "The seller name could not be extracted.", diagnostics);
        AddMissing(data.Buyer.Name, "PXAEINV033", "The buyer name could not be extracted.", diagnostics);
        if (data.TaxInclusiveTotal is null && data.PayableAmount is null)
            diagnostics.Add(Warning("PXAEINV034", "No invoice total could be extracted."));
        if (data.TaxExclusiveTotal is { } net && data.TaxTotal is { } tax &&
            data.TaxInclusiveTotal is { } gross && Math.Abs(net + tax - gross) > 0.02m)
        {
            diagnostics.Add(Warning(
                "PXAEINV035",
                "Tax-exclusive total plus tax does not equal the tax-inclusive total."));
        }
    }

    private static void AddMissing(
        string? value,
        string code,
        string message,
        ICollection<PxaEInvoiceDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value)) diagnostics.Add(Warning(code, message));
    }

    private static XElement? First(XContainer? parent, string localName) =>
        parent?.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static IEnumerable<XElement> Children(XContainer? parent, string localName) =>
        parent?.Elements().Where(element => element.Name.LocalName == localName) ?? [];

    private static string? Value(XElement? element)
    {
        var value = element?.Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value.Length <= 4_096 ? value : value[..4_096];
    }

    private static string? AttributeValue(XElement? element, string localName) =>
        Value(element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName));

    private static string? Value(XAttribute? attribute)
    {
        var value = attribute?.Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value.Length <= 256 ? value : value[..256];
    }

    private static decimal? DecimalValue(XElement? element) =>
        decimal.TryParse(Value(element), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    public static PdfAssociatedFile Attach(
        PdfDocument document,
        ReadOnlyMemory<byte> xml,
        PxaEInvoiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        var validation = Validate(xml, options);
        if (!validation.IsStructurallyValid)
        {
            var message = string.Join(
                " ",
                validation.Diagnostics
                    .Where(static item => item.Severity == "error")
                    .Select(static item => item.Code + ": " + item.Message));
            throw new InvalidDataException(message);
        }

        var descriptor = DescribeAttachment(options);
        return document.AddAssociatedFile(
            xml,
            descriptor.FileName,
            descriptor.MimeType,
            descriptor.Relationship,
            descriptor.Description,
            descriptor.XmpMetadata);
    }

    /// <summary>
    /// Describes how the invoice XML has to appear inside a PDF for the requested standard.
    /// Embedding into an existing PDF uses this so a hybrid invoice carries the same filename,
    /// relationship and PDF/A extension schema no matter which entry point produced it.
    /// </summary>
    public static PxaEInvoiceAttachmentDescriptor DescribeAttachment(PxaEInvoiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.Standard))
            throw new ArgumentOutOfRangeException(nameof(options), "The electronic-invoice standard is not supported.");

        var profile = NormalizeProfile(options.Profile);
        if (profile is null)
            throw new ArgumentException("A profile is required to describe an invoice attachment.", nameof(options));

        var fileName = string.IsNullOrWhiteSpace(options.FileName)
            ? DefaultFileName(options.Standard)
            : options.FileName.Trim();
        return new PxaEInvoiceAttachmentDescriptor(
            fileName,
            "application/xml",
            PdfAssociatedFileRelationship.Alternative,
            options.Standard + " " + options.Version + " " + profile + " invoice XML",
            CreateXmpMetadata(options.Standard, fileName, profile));
    }

    private static void ValidateOptions(
        PxaEInvoiceOptions options,
        ICollection<PxaEInvoiceDiagnostic> diagnostics)
    {
        if (!Enum.IsDefined(options.Standard))
        {
            diagnostics.Add(Error("PXAEINV016", "The electronic-invoice standard is not supported."));
            return;
        }
        if (string.IsNullOrWhiteSpace(options.Version) || options.Version.Length > 32)
            diagnostics.Add(Error("PXAEINV007", "A version between 1 and 32 characters is required."));
        if (string.IsNullOrWhiteSpace(options.Profile) || options.Profile.Length > 64)
            diagnostics.Add(Error("PXAEINV008", "A profile between 1 and 64 characters is required."));

        var expectedVersion = CurrentVersion(options.Standard);
        if (!string.IsNullOrWhiteSpace(options.Version) &&
            !string.Equals(options.Version.Trim(), expectedVersion, StringComparison.Ordinal))
        {
            diagnostics.Add(Error(
                "PXAEINV012",
                $"PXA currently supports {options.Standard} version '{expectedVersion}', not '{options.Version.Trim()}'."));
        }

        var normalizedProfile = NormalizeProfile(options.Profile);
        var validProfile = normalizedProfile is not null &&
            (options.Standard == PxaEInvoiceStandard.XRechnung
                ? normalizedProfile == "XRECHNUNG"
                : HybridProfiles.Contains(normalizedProfile));
        if (!string.IsNullOrWhiteSpace(options.Profile) && !validProfile)
        {
            diagnostics.Add(Error(
                "PXAEINV013",
                $"Profile '{options.Profile.Trim()}' is not supported for {options.Standard}."));
        }

        if (!string.IsNullOrWhiteSpace(options.FileName) &&
            !string.Equals(options.FileName.Trim(), DefaultFileName(options.Standard), StringComparison.Ordinal))
        {
            diagnostics.Add(Error(
                "PXAEINV014",
                $"{options.Standard} invoice XML must use filename '{DefaultFileName(options.Standard)}'."));
        }
    }

    private static string? DetectSyntax(XElement root)
    {
        if (root.Name.LocalName == "CrossIndustryInvoice" &&
            root.Name.NamespaceName.Contains("CrossIndustryInvoice", StringComparison.Ordinal))
            return "CII";
        if (root.Name.LocalName == "Invoice" &&
            root.Name.NamespaceName == "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2")
            return "UBL-Invoice";
        if (root.Name.LocalName == "CreditNote" &&
            root.Name.NamespaceName == "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2")
            return "UBL-CreditNote";
        return null;
    }

    private static string? FindGuidelineIdentifier(XDocument document, string syntax)
    {
        if (syntax == "CII")
        {
            return document
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "GuidelineSpecifiedDocumentContextParameter")?
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "ID")?
                .Value
                .Trim();
        }

        return document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "CustomizationID")?
            .Value
            .Trim();
    }

    private static void ValidateStandardMatch(
        PxaEInvoiceStandard standard,
        string syntax,
        string? guideline,
        ICollection<PxaEInvoiceDiagnostic> diagnostics)
    {
        if (standard is PxaEInvoiceStandard.FacturX or PxaEInvoiceStandard.Zugferd)
        {
            if (syntax != "CII")
            {
                diagnostics.Add(Error(
                    "PXAEINV009",
                    "Factur-X and ZUGFeRD require UN/CEFACT CrossIndustryInvoice syntax."));
                return;
            }

            if (!string.IsNullOrWhiteSpace(guideline) &&
                !guideline.Contains("factur-x", StringComparison.OrdinalIgnoreCase) &&
                !guideline.Contains("ferd", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Error(
                    "PXAEINV010",
                    "The CII guideline identifier does not identify Factur-X or ZUGFeRD."));
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(guideline) &&
            !guideline.Contains("xrechnung", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Error(
                "PXAEINV011",
                "The customization identifier does not identify XRechnung."));
        }
    }

    private static PxaEInvoiceValidationReport Report(
        bool valid,
        string? syntax,
        string? guideline,
        IReadOnlyList<PxaEInvoiceDiagnostic> diagnostics) =>
        new(valid, false, syntax, guideline, diagnostics);

    private static PxaEInvoiceDiagnostic Error(string code, string message) =>
        new(code, "error", message);

    private static PxaEInvoiceDiagnostic Warning(string code, string message) =>
        new(code, "warning", message);

    private static string CurrentVersion(PxaEInvoiceStandard standard) =>
        standard switch
        {
            PxaEInvoiceStandard.FacturX => CurrentFacturXVersion,
            PxaEInvoiceStandard.Zugferd => CurrentZugferdVersion,
            PxaEInvoiceStandard.XRechnung => CurrentXRechnungVersion,
            _ => throw new ArgumentOutOfRangeException(nameof(standard))
        };

    private static string? NormalizeProfile(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
            return null;
        var normalized = profile.Trim().ToUpperInvariant().Replace('_', ' ');
        return normalized switch
        {
            "BASICWL" => "BASIC WL",
            "EN16931" => "EN 16931",
            "EXTENDED CTC FR" => "EXTENDED-CTC-FR",
            _ => normalized
        };
    }

    private static string? DetectProfile(string? guideline)
    {
        if (string.IsNullOrWhiteSpace(guideline))
            return null;
        var value = guideline.ToUpperInvariant();
        if (value.Contains("XRECHNUNG", StringComparison.Ordinal)) return "XRECHNUNG";
        if (value.Contains("EXTENDED-CTC-FR", StringComparison.Ordinal)) return "EXTENDED-CTC-FR";
        if (value.Contains("EXTENDED", StringComparison.Ordinal)) return "EXTENDED";
        if (value.Contains("EN16931", StringComparison.Ordinal) || value.Contains("EN 16931", StringComparison.Ordinal)) return "EN 16931";
        if (value.Contains("BASICWL", StringComparison.Ordinal) || value.Contains("BASIC_WL", StringComparison.Ordinal)) return "BASIC WL";
        if (value.Contains("BASIC", StringComparison.Ordinal)) return "BASIC";
        if (value.Contains("MINIMUM", StringComparison.Ordinal)) return "MINIMUM";
        return null;
    }

    private static PdfAssociatedFileXmpMetadata CreateXmpMetadata(
        PxaEInvoiceStandard standard,
        string fileName,
        string profile)
    {
        var prefix = "fx";
        var schemaName = "Factur-X PDFA Extension Schema";
        var namespaceUri = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";
        var xmpVersion = "1.0";

        return new PdfAssociatedFileXmpMetadata
        {
            SchemaName = schemaName,
            NamespaceUri = namespaceUri,
            Prefix = prefix,
            Properties =
            [
                Property("DocumentFileName", fileName, "The name of the embedded XML document."),
                Property("DocumentType", "INVOICE", "The type of the hybrid document."),
                Property("Version", xmpVersion, "The version of the electronic-invoice data specification."),
                Property("ConformanceLevel", profile, "The conformance profile of the embedded invoice XML.")
            ]
        };
    }

    private static PdfAssociatedFileXmpProperty Property(string name, string value, string description) =>
        new(name, value, "Text", "external", description);

    private static string DefaultFileName(PxaEInvoiceStandard standard) =>
        standard switch
        {
            PxaEInvoiceStandard.FacturX => "factur-x.xml",
            PxaEInvoiceStandard.Zugferd => "factur-x.xml",
            PxaEInvoiceStandard.XRechnung => "xrechnung.xml",
            _ => throw new ArgumentOutOfRangeException(nameof(standard))
        };
}
