using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Pdf;

public static class PdfTrustMigrationCatalog
{
    private const string TrustGuide = "/docs/guides/PVAD.Pdf-Trust-Generator-Examples.html";

    private static readonly IReadOnlyDictionary<string, PdfTrustProviderProfile> Profiles =
        new[]
        {
            Profile(PdfMigrationProviderKeys.ActivePdf, "ActivePDF", "Signature", "Encryption", "Security"),
            Profile(PdfMigrationProviderKeys.Apryse, "Apryse", "DigitalSignatureField", "SignOnNextSave", "Timestamp", "ValidateSignature", "OpenEncrypted", "SecurityHandler", "Redactor", "PDFACompliance", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.AsposePdf, "Aspose.PDF", "SignatureField", "PdfFileSignature", "TimestampSettings", "ValidateSignature", "OpenEncrypted", "PdfFileSecurity", "RedactionAnnotation", "ApplyRedactions", "Convert", "PdfAConversion", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.DevExpressPdf, "DevExpress PDF", "PdfDocumentSigner", "Timestamp", "ValidateSignature", "OpenEncrypted", "PdfEncryptionOptions", "ApplyRedactions", "PdfACompatibility", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.DsPdf, "DsPdf", "Sign", "Timestamp", "ValidateSignature", "OpenEncrypted", "Security", "ApplyRedactions", "SaveAsPdfA", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.FoxitPdf, "Foxit PDF SDK", "Signature", "Sign", "Timestamp", "ValidateSignature", "OpenEncrypted", "SecurityHandler", "Redaction", "ApplyRedactions", "PDFCompliance", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.GemBoxPdf, "GemBox.Pdf", "PdfSignature", "Timestamp", "ValidateSignature", "OpenEncrypted", "PdfEncryption", "ApplyRedactions", "PdfConformanceLevel", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.IronPdf, "IronPDF", "SignWithDigitalSignature", "SecuritySettings", "ConvertToPdfA"),
            Profile(PdfMigrationProviderKeys.IText7, "iText 7", "PdfSigner", "SignDetached", "TSAClient", "LtvVerification", "OpenEncrypted", "SetStandardEncryption", "PdfCleanUpTool", "PdfADocument", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.LeadtoolsPdf, "LEADTOOLS PDF", "Signature", "Encryption", "Security", "PDF/A"),
            Profile(PdfMigrationProviderKeys.Nutrient, "Nutrient SDK", "SignPDF", "Timestamp", "ValidateSignature", "OpenEncrypted", "Encrypt", "ApplyRedaction", "ConvertToPDFA", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.PdfKitNet, "PDFKit.NET", "Signature", "Encryption", "Security"),
            Profile(PdfMigrationProviderKeys.PdfTools, "Pdftools", "Sign", "Timestamp", "ValidateSignature", "OpenEncrypted", "Encrypt", "Redact", "Conformance", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.PdfToolsToolbox, "Pdftools Toolbox", "Signature", "Encryption", "Redaction", "Conformance"),
            Profile(PdfMigrationProviderKeys.SpirePdf, "Spire.PDF", "PdfSignature", "PdfCertificate", "Timestamp", "ValidateSignature", "OpenEncrypted", "PdfSecurity", "PdfRedactionArea", "ApplyRedactions", "PdfConformanceLevel", "PdfUaValidation"),
            Profile(PdfMigrationProviderKeys.SyncfusionPdf, "Syncfusion PDF", "PdfSignature", "PdfCertificate", "Timestamp", "ValidateSignature", "OpenEncrypted", "PdfSecurity", "PdfRedaction", "ApplyRedactions", "PdfConformanceLevel", "PdfUaValidation")
        }.ToDictionary(static profile => profile.ProviderKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PdfTrustProviderProfile> All { get; } = Profiles.Values
        .OrderBy(static profile => profile.ProviderKey, StringComparer.Ordinal)
        .ToArray();

    public static PdfTrustProviderProfile Get(string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        return Profiles.TryGetValue(providerKey, out var profile)
            ? profile
            : throw new KeyNotFoundException($"Unknown PVAD PDF migration provider key '{providerKey}'.");
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
