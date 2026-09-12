using PXA.Migration.Abstractions;

namespace PXA.Migration.Pdf.Viewer.Nutrient;

internal static class NutrientViewerTrustEvidence
{
    private const string ProviderKey = "nutrient-viewer";
    private const string TrustGuide = "/docs/guides/PXA.Pdf-Trust-Generator-Examples.html";

    public static PdfTrustMigrationReport FromViewerSource(string source)
    {
        var evidence = new List<PdfTrustMigrationEvidence>();
        AddSourceEvidence(evidence, source, PdfTrustCapability.DigitalSignature,
            ["signature", "signatures"], "CANMIGNUTWEB040", PdfTrustSourceSemantics.Unknown,
            "PdfTrustPostProcessingPipeline.Signing");
        AddSourceEvidence(evidence, source, PdfTrustCapability.SignatureValidation,
            ["verifySignature", "validateSignature"], "CANMIGNUTWEB041", PdfTrustSourceSemantics.ComplianceValidation,
            "IPdfTrustInspector.InspectAsync");
        AddSourceEvidence(evidence, source, PdfTrustCapability.SecureRedaction,
            ["redaction", "redact"], "CANMIGNUTWEB042", PdfTrustSourceSemantics.VisualRedactionOnly,
            null);

        return Report("viewer-source", evidence);
    }

    public static PdfTrustMigrationReport FromInstantJson(IEnumerable<NutrientViewerDiagnostic> diagnostics)
    {
        var codes = diagnostics.Select(static item => item.Code).ToHashSet(StringComparer.Ordinal);
        var evidence = new List<PdfTrustMigrationEvidence>();
        if (codes.Contains("CANMIGNUTJSON020"))
        {
            evidence.Add(Evidence(
                PdfTrustCapability.SecureRedaction,
                PdfTrustMigrationDisposition.Unsupported,
                "pspdfkit/markup/redaction",
                null,
                PdfTrustSourceSemantics.VisualRedactionOnly,
                "CANMIGNUTJSON020",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sourceGuarantee"] = "visual-mark-only",
                    ["contentRemoved"] = "false"
                }));
        }
        if (codes.Contains("CANMIGNUTJSON030") || codes.Contains("CANMIGNUTJSON046"))
        {
            evidence.Add(Evidence(
                PdfTrustCapability.DigitalSignature,
                PdfTrustMigrationDisposition.Unsupported,
                "saved signature appearance or signature field value",
                null,
                PdfTrustSourceSemantics.SignatureAppearanceOnly,
                codes.Contains("CANMIGNUTJSON030") ? "CANMIGNUTJSON030" : "CANMIGNUTJSON046"));
        }

        return Report("viewer-instant-json", evidence);
    }

    private static void AddSourceEvidence(
        ICollection<PdfTrustMigrationEvidence> evidence,
        string source,
        PdfTrustCapability capability,
        IReadOnlyList<string> markers,
        string diagnosticId,
        PdfTrustSourceSemantics semantics,
        string? targetOperation)
    {
        var detected = markers
            .Where(marker => source.Contains(marker, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (detected.Length == 0)
            return;

        evidence.Add(Evidence(
            capability,
            PdfTrustMigrationPolicy.ResolveDisposition(
                semantics,
                targetHasEquivalentSemantics: semantics != PdfTrustSourceSemantics.VisualRedactionOnly,
                providerOptionsAreComplete: false,
                targetCanValidateEndToEnd: true),
            string.Join(", ", detected),
            targetOperation,
            semantics,
            diagnosticId));
    }

    private static PdfTrustMigrationEvidence Evidence(
        PdfTrustCapability capability,
        PdfTrustMigrationDisposition disposition,
        string sourceOperation,
        string? targetOperation,
        PdfTrustSourceSemantics semantics,
        string diagnosticId,
        IReadOnlyDictionary<string, string>? providerOptions = null) => new()
    {
        Capability = capability,
        Disposition = disposition,
        SourceOperation = sourceOperation,
        TargetOperation = targetOperation,
        SourceSemantics = semantics,
        DiagnosticId = diagnosticId,
        DocumentationPath = DocumentationFor(capability),
        ProviderOptions = providerOptions ?? new Dictionary<string, string>(StringComparer.Ordinal)
    };

    private static PdfTrustMigrationReport Report(
        string migrationKind,
        IReadOnlyList<PdfTrustMigrationEvidence> evidence) => new()
    {
        ProviderKey = ProviderKey,
        MigrationKind = migrationKind,
        Evidence = evidence
    };

    private static string DocumentationFor(PdfTrustCapability capability) => capability switch
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
