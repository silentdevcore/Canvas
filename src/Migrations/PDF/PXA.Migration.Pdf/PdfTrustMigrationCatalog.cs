using PXA.Migration.Abstractions;

namespace PXA.Migration.Pdf;

public static class PdfTrustMigrationCatalog
{
    private const string TrustGuide = "/docs/guides/PXA.Pdf-Trust-Generator-Examples.html";

    private static readonly IReadOnlyDictionary<string, PdfTrustProviderProfile> Profiles =
        new[]
        {
            Profile(PdfMigrationProviderKeys.ActivePdf, "ActivePDF", "Signature", "Encryption", "Security"),
            Profile(PdfMigrationProviderKeys.Apryse, "Apryse", "DigitalSignatureField", "SecurityHandler", "Redactor", "PDFACompliance"),
            Profile(PdfMigrationProviderKeys.AsposePdf, "Aspose.PDF", "SignatureField", "PdfFileSecurity", "RedactionAnnotation", "Convert"),
            Profile(PdfMigrationProviderKeys.DevExpressPdf, "DevExpress PDF", "PdfDocumentSigner", "PdfEncryptionOptions", "PdfACompatibility"),
            Profile(PdfMigrationProviderKeys.DsPdf, "DsPdf", "Sign", "Security", "ApplyRedactions", "SaveAsPdfA"),
            Profile(PdfMigrationProviderKeys.FoxitPdf, "Foxit PDF SDK", "Signature", "SecurityHandler", "Redaction", "PDFCompliance"),
            Profile(PdfMigrationProviderKeys.GemBoxPdf, "GemBox.Pdf", "PdfSignature", "PdfEncryption", "PdfConformanceLevel"),
            Profile(PdfMigrationProviderKeys.IronPdf, "IronPDF", "SignWithDigitalSignature", "SecuritySettings", "ConvertToPdfA"),
            Profile(PdfMigrationProviderKeys.IText7, "iText 7", "PdfSigner", "TSAClient", "LtvVerification", "SetStandardEncryption", "PdfCleanUpTool", "PdfADocument"),
            Profile(PdfMigrationProviderKeys.LeadtoolsPdf, "LEADTOOLS PDF", "Signature", "Encryption", "Security", "PDF/A"),
            Profile(PdfMigrationProviderKeys.Nutrient, "Nutrient SDK", "SignPDF", "Timestamp", "ApplyRedaction", "Encrypt", "ConvertToPDFA"),
            Profile(PdfMigrationProviderKeys.PdfKitNet, "PDFKit.NET", "Signature", "Encryption", "Security"),
            Profile(PdfMigrationProviderKeys.PdfTools, "Pdftools", "Sign", "Timestamp", "Encrypt", "Redact", "Conformance"),
            Profile(PdfMigrationProviderKeys.PdfToolsToolbox, "Pdftools Toolbox", "Signature", "Encryption", "Redaction", "Conformance"),
            Profile(PdfMigrationProviderKeys.SpirePdf, "Spire.PDF", "PdfSignature", "PdfCertificate", "PdfSecurity", "PdfRedactionArea", "PdfConformanceLevel"),
            Profile(PdfMigrationProviderKeys.SyncfusionPdf, "Syncfusion PDF", "PdfSignature", "PdfCertificate", "PdfSecurity", "PdfRedaction", "PdfConformanceLevel")
        }.ToDictionary(static profile => profile.ProviderKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PdfTrustProviderProfile> All { get; } = Profiles.Values
        .OrderBy(static profile => profile.ProviderKey, StringComparer.Ordinal)
        .ToArray();

    public static PdfTrustProviderProfile Get(string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        return Profiles.TryGetValue(providerKey, out var profile)
            ? profile
            : throw new KeyNotFoundException($"Unknown PXA PDF migration provider key '{providerKey}'.");
    }

    public static bool TryGet(string providerKey, out PdfTrustProviderProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        return Profiles.TryGetValue(providerKey, out profile!);
    }

    private static PdfTrustProviderProfile Profile(string providerKey, string displayName, params string[] apiMarkers)
    {
        var markers = apiMarkers.Distinct(StringComparer.Ordinal).ToArray();
        return new PdfTrustProviderProfile
        {
            ProviderKey = providerKey,
            DisplayName = displayName,
            MigrationKind = "code",
            Capabilities = Enum.GetValues<PdfTrustCapability>()
                .Select(capability => new PdfTrustProviderCapability
                {
                    Capability = capability,
                    DefaultDisposition = PdfTrustMigrationDisposition.ReviewRequired,
                    DetectedApiMarkers = MarkersFor(capability, markers),
                    DocumentationPath = DocumentationFor(capability)
                })
                .ToArray()
        };
    }

    private static IReadOnlyList<string> MarkersFor(PdfTrustCapability capability, IReadOnlyList<string> markers) =>
        markers.Where(marker => capability switch
        {
            PdfTrustCapability.DigitalSignature => ContainsAny(marker, "Sign", "Certificate"),
            PdfTrustCapability.Timestamp => ContainsAny(marker, "Timestamp", "TSA", "Ltv"),
            PdfTrustCapability.SignatureValidation => ContainsAny(marker, "Verif", "Valid", "Ltv"),
            PdfTrustCapability.Encryption => ContainsAny(marker, "Encrypt", "Security"),
            PdfTrustCapability.SecureRedaction => ContainsAny(marker, "Redact", "CleanUp"),
            PdfTrustCapability.PdfA => ContainsAny(marker, "PDFA", "PdfA", "Conformance", "Compliance"),
            PdfTrustCapability.PdfUa => ContainsAny(marker, "PDFUA", "PdfUa", "Access", "Tag"),
            _ => false
        }).ToArray();

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    public static string DocumentationFor(PdfTrustCapability capability) => capability switch
    {
        PdfTrustCapability.DigitalSignature or PdfTrustCapability.Timestamp or PdfTrustCapability.SignatureValidation =>
            $"{TrustGuide}#pades-b-t-lt-and-lta",
        PdfTrustCapability.Encryption => $"{TrustGuide}#aes-256-password-encryption",
        PdfTrustCapability.SecureRedaction => $"{TrustGuide}#redaction-and-sanitization",
        PdfTrustCapability.PdfA => $"{TrustGuide}#pdfa-2b-and-pdfa-3b",
        PdfTrustCapability.PdfUa => $"{TrustGuide}#pdfua-1",
        _ => TrustGuide
    };
}
