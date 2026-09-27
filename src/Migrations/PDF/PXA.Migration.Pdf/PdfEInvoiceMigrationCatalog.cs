using PXA.Migration.Abstractions;

namespace PXA.Migration.Pdf;

/// <summary>
/// Electronic-invoice capability mappings for every PDF code-migration provider.
///
/// The mappings are keyed on API identifiers a customer's source may contain, not on a claim about
/// what each vendor's product line offers. That distinction matters: PXA can honestly say "if your
/// code calls this, here is the PXA equivalent and what a human still has to confirm", while it
/// cannot honestly assert which invoice features a third-party SDK ships today.
///
/// Every capability of every provider carries a disposition and a rationale, so a source API can
/// never become a silent TODO. Nothing here is a conformance claim: a migrated call produces PXA
/// invoice output whose conformance is then established by the pinned official artifact sets.
/// </summary>
public static class PdfEInvoiceMigrationCatalog
{
    private const string Guide = "/docs/guides/PXA.EInvoicing.html";

    /// <summary>
    /// Invoice generation always needs review: the source rarely states a standard version and
    /// profile explicitly, and PXA must not guess which one a customer is obliged to issue.
    /// </summary>
    private const string GenerationRationale =
        "Mapped onto EInvoice.GenerateXml/CreatePdf. Review is required because the standard " +
        "version and profile are a business obligation the source code usually leaves implicit.";

    private const string NoInvoiceApiRationale =
        "No invoice API of this kind was identified for this provider. PXA does not invent one; " +
        "build the invoice with the typed PXA invoice model instead.";

    private static readonly IReadOnlyDictionary<string, PdfEInvoiceProviderProfile> Profiles =
        new[]
        {
            // iText's invoice add-on is the one widely used .NET API that names the hybrid
            // invoice standards directly, so its markers map onto PXA's invoice generation.
            Profile(
                PdfMigrationProviderKeys.IText7,
                "iText 7",
                attachmentMarkers: ["PdfFileSpec", "AddFileAttachment", "AddAssociatedFile", "PdfADocument", "PdfAConformanceLevel"],
                facturXMarkers: ["FacturX", "PdfInvoice", "InvoiceProfile"],
                zugferdMarkers: ["ZugferdDocument", "ZugferdConformanceLevel", "ZugferdXMP"],
                xrechnungMarkers: [],
                extractionMarkers: ["GetEmbeddedFiles", "PdfNameTree", "EmbeddedFiles"],
                validationMarkers: []),
            Profile(
                PdfMigrationProviderKeys.AsposePdf,
                "Aspose.PDF",
                attachmentMarkers: ["EmbeddedFileCollection", "FileSpecification", "Document.EmbeddedFiles", "PdfFormat.PDF_A_3B", "PdfAConversion"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["EmbeddedFileCollection", "FileSpecification.Contents"],
                validationMarkers: ["Validate", "PdfFormatConversionOptions"]),
            Profile(
                PdfMigrationProviderKeys.Apryse,
                "Apryse",
                attachmentMarkers: ["FileSpec", "FileSpec.Create", "PDFA3", "PDFACompliance"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["FileSpec.Export", "NameTree"],
                validationMarkers: ["PDFACompliance"]),
            Profile(
                PdfMigrationProviderKeys.DevExpressPdf,
                "DevExpress PDF",
                attachmentMarkers: ["PdfFileAttachment", "PdfEmbeddedFileSpecification", "PdfACompatibility.PdfA3b"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["PdfEmbeddedFileSpecification", "PdfDocumentFacade"],
                validationMarkers: []),
            Profile(
                PdfMigrationProviderKeys.SpirePdf,
                "Spire.PDF",
                attachmentMarkers: ["PdfAttachmentCollection", "PdfAttachment", "PdfConformanceLevel.Pdf_A3B"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["PdfAttachmentCollection", "Attachments"],
                validationMarkers: []),
            Profile(
                PdfMigrationProviderKeys.SyncfusionPdf,
                "Syncfusion PDF",
                attachmentMarkers: ["PdfAttachment", "PdfAttachmentCollection", "PdfConformanceLevel.Pdf_A3B"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["PdfLoadedDocument.Attachments"],
                validationMarkers: []),
            Profile(
                PdfMigrationProviderKeys.GemBoxPdf,
                "GemBox.Pdf",
                attachmentMarkers: ["PdfEmbeddedFile", "EmbeddedFiles", "PdfConformanceLevel.PdfA3b"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["EmbeddedFiles"],
                validationMarkers: []),
            Profile(
                PdfMigrationProviderKeys.DsPdf,
                "DsPdf",
                attachmentMarkers: ["EmbeddedFileStream", "FileSpecification", "SaveAsPdfA", "PdfAConformanceLevel"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["EmbeddedFileStream"],
                validationMarkers: []),
            Profile(
                PdfMigrationProviderKeys.FoxitPdf,
                "Foxit PDF SDK",
                attachmentMarkers: ["Attachments", "FileSpec", "PDFCompliance"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["Attachments", "FileSpec"],
                validationMarkers: ["PDFCompliance"]),
            Profile(
                PdfMigrationProviderKeys.Nutrient,
                "Nutrient SDK",
                attachmentMarkers: ["AddEmbeddedFile", "EmbeddedFiles", "ConvertToPDFA", "PdfAConformance"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["EmbeddedFiles"],
                validationMarkers: ["ValidatePDFA"]),
            Profile(
                PdfMigrationProviderKeys.PdfTools,
                "Pdftools",
                attachmentMarkers: ["FileAttachment", "Conformance.PdfA3B", "Converter"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["FileAttachment"],
                validationMarkers: ["Validator", "Conformance"]),
            Profile(
                PdfMigrationProviderKeys.PdfToolsToolbox,
                "Pdftools Toolbox",
                attachmentMarkers: ["FileReference", "EmbeddedFile", "Conformance"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["EmbeddedFile"],
                validationMarkers: ["Conformance"]),
            Profile(
                PdfMigrationProviderKeys.LeadtoolsPdf,
                "LEADTOOLS PDF",
                attachmentMarkers: ["PDFFile", "PDFA"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: [],
                validationMarkers: []),
            Profile(
                PdfMigrationProviderKeys.IronPdf,
                "IronPDF",
                attachmentMarkers: ["Attachments", "AddAttachment", "ConvertToPdfA"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["Attachments"],
                validationMarkers: []),
            Profile(
                PdfMigrationProviderKeys.ActivePdf,
                "ActivePDF",
                attachmentMarkers: ["AddFileAttachment", "PDFA"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: [],
                validationMarkers: []),
            Profile(
                PdfMigrationProviderKeys.PdfKitNet,
                "PDFKit.NET",
                attachmentMarkers: ["EmbeddedFile", "FileAttachment"],
                facturXMarkers: [],
                zugferdMarkers: [],
                xrechnungMarkers: [],
                extractionMarkers: ["EmbeddedFile"],
                validationMarkers: [])
        }.ToDictionary(static profile => profile.ProviderKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PdfEInvoiceProviderProfile> All { get; } = Profiles.Values
        .OrderBy(static profile => profile.ProviderKey, StringComparer.Ordinal)
        .ToArray();

    public static PdfEInvoiceProviderProfile Get(string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        return Profiles.TryGetValue(providerKey, out var profile)
            ? profile
            : throw new KeyNotFoundException(
                $"Unknown PXA PDF migration provider key '{providerKey}'.");
    }

    public static bool TryGet(string providerKey, out PdfEInvoiceProviderProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        return Profiles.TryGetValue(providerKey, out profile!);
    }

    /// <summary>Providers that expose at least one electronic-invoice capability PXA can map.</summary>
    public static IReadOnlyList<PdfEInvoiceProviderProfile> WithInvoiceCapabilities { get; } = All
        .Where(static profile => profile.Capabilities.Any(static capability =>
            capability.Disposition != PdfTrustMigrationDisposition.Unsupported))
        .ToArray();

    public static string DocumentationFor(PdfEInvoiceCapability capability) => capability switch
    {
        PdfEInvoiceCapability.FacturX => $"{Guide}#factur-x",
        PdfEInvoiceCapability.Zugferd => $"{Guide}#zugferd",
        PdfEInvoiceCapability.XRechnung => $"{Guide}#xrechnung",
        PdfEInvoiceCapability.PdfA3Attachment => $"{Guide}#attaching-to-an-existing-pdf",
        PdfEInvoiceCapability.InvoiceExtraction => $"{Guide}#extraction",
        PdfEInvoiceCapability.InvoiceValidation => $"{Guide}#validation",
        _ => Guide
    };

    private static PdfEInvoiceProviderProfile Profile(
        string providerKey,
        string displayName,
        IReadOnlyList<string> attachmentMarkers,
        IReadOnlyList<string> facturXMarkers,
        IReadOnlyList<string> zugferdMarkers,
        IReadOnlyList<string> xrechnungMarkers,
        IReadOnlyList<string> extractionMarkers,
        IReadOnlyList<string> validationMarkers) => new()
        {
            ProviderKey = providerKey,
            DisplayName = displayName,
            Capabilities =
            [
                Generation(PdfEInvoiceCapability.FacturX, facturXMarkers),
                Generation(PdfEInvoiceCapability.Zugferd, zugferdMarkers),
                Generation(PdfEInvoiceCapability.XRechnung, xrechnungMarkers),
                Attachment(attachmentMarkers),
                Extraction(extractionMarkers),
                Validation(validationMarkers)
            ]
        };

    private static PdfEInvoiceProviderCapability Generation(
        PdfEInvoiceCapability capability,
        IReadOnlyList<string> markers) => markers.Count == 0
        ? new PdfEInvoiceProviderCapability
        {
            Capability = capability,
            Disposition = PdfTrustMigrationDisposition.Unsupported,
            Rationale = NoInvoiceApiRationale,
            DocumentationPath = DocumentationFor(capability)
        }
        : new PdfEInvoiceProviderCapability
        {
            Capability = capability,
            Disposition = PdfTrustMigrationDisposition.ReviewRequired,
            DetectedApiMarkers = markers,
            PxaEquivalent = "PXA.Generator.EInvoice.GenerateXml / EInvoice.CreatePdf",
            Rationale = GenerationRationale,
            DocumentationPath = DocumentationFor(capability)
        };

    /// <summary>
    /// Embedding a file as a PDF/A-3 associated file is the one mapping with no judgement in it:
    /// the bytes, filename, media type and relationship all transfer directly, so it is automatic.
    /// </summary>
    private static PdfEInvoiceProviderCapability Attachment(IReadOnlyList<string> markers) =>
        markers.Count == 0
            ? new PdfEInvoiceProviderCapability
            {
                Capability = PdfEInvoiceCapability.PdfA3Attachment,
                Disposition = PdfTrustMigrationDisposition.Unsupported,
                Rationale = NoInvoiceApiRationale,
                DocumentationPath = DocumentationFor(PdfEInvoiceCapability.PdfA3Attachment)
            }
            : new PdfEInvoiceProviderCapability
            {
                Capability = PdfEInvoiceCapability.PdfA3Attachment,
                Disposition = PdfTrustMigrationDisposition.Automatic,
                DetectedApiMarkers = markers,
                PxaEquivalent =
                    "PdfDocument.AddAssociatedFile for a new document, or the authenticated " +
                    "/api/pxa/v1/pdf/e-invoices/attach operation for an existing one",
                Rationale =
                    "Embedding a file with a filename, media type and document relationship " +
                    "transfers directly onto PXA's associated-file API; no invoice semantics are " +
                    "assumed by the mapping itself.",
                DocumentationPath = DocumentationFor(PdfEInvoiceCapability.PdfA3Attachment)
            };

    private static PdfEInvoiceProviderCapability Extraction(IReadOnlyList<string> markers) =>
        markers.Count == 0
            ? new PdfEInvoiceProviderCapability
            {
                Capability = PdfEInvoiceCapability.InvoiceExtraction,
                Disposition = PdfTrustMigrationDisposition.Unsupported,
                Rationale = NoInvoiceApiRationale,
                DocumentationPath = DocumentationFor(PdfEInvoiceCapability.InvoiceExtraction)
            }
            : new PdfEInvoiceProviderCapability
            {
                Capability = PdfEInvoiceCapability.InvoiceExtraction,
                Disposition = PdfTrustMigrationDisposition.Automatic,
                DetectedApiMarkers = markers,
                PxaEquivalent =
                    "PdfAssociatedFileExtractor, or /api/pxa/v1/pdf/e-invoices/inspect and /extract",
                Rationale =
                    "Reading an embedded file back is byte-preserving on both sides, and PXA " +
                    "additionally verifies the declared size and checksum.",
                DocumentationPath = DocumentationFor(PdfEInvoiceCapability.InvoiceExtraction)
            };

    /// <summary>
    /// A source validation call is never mapped to an automatic one. The source may have validated
    /// something entirely different - PDF/A structure rather than invoice rules - and PXA must not
    /// let a migration turn that into an invoice-conformance claim.
    /// </summary>
    private static PdfEInvoiceProviderCapability Validation(IReadOnlyList<string> markers) =>
        markers.Count == 0
            ? new PdfEInvoiceProviderCapability
            {
                Capability = PdfEInvoiceCapability.InvoiceValidation,
                Disposition = PdfTrustMigrationDisposition.Unsupported,
                Rationale = NoInvoiceApiRationale,
                DocumentationPath = DocumentationFor(PdfEInvoiceCapability.InvoiceValidation)
            }
            : new PdfEInvoiceProviderCapability
            {
                Capability = PdfEInvoiceCapability.InvoiceValidation,
                Disposition = PdfTrustMigrationDisposition.ReviewRequired,
                DetectedApiMarkers = markers,
                PxaEquivalent = "/api/pxa/v1/pdf/e-invoices/validate",
                Rationale =
                    "The detected call may validate PDF/A structure rather than invoice rules. " +
                    "PXA reports structural recognition, its own business validation and official " +
                    "conformance separately, so a reviewer has to confirm what was being checked.",
                DocumentationPath = DocumentationFor(PdfEInvoiceCapability.InvoiceValidation)
            };
}
