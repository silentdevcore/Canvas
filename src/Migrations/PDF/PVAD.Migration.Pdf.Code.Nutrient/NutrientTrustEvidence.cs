using Microsoft.CodeAnalysis.CSharp.Syntax;
using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Pdf.Code.Nutrient;

internal static class NutrientTrustEvidence
{
    private const string ProviderKey = "nutrient";
    private const string TrustGuide = "/docs/guides/PVAD.Pdf-Trust-Generator-Examples.html";

    public static PdfTrustMigrationReport Scan(CompilationUnitSyntax root)
    {
        var invocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(static invocation => new DetectedOperation(
                invocation.Expression is MemberAccessExpressionSyntax member
                    ? member.Name.Identifier.ValueText
                    : string.Empty,
                invocation.ArgumentList.Arguments.ToString(),
                SourceLocation(invocation)))
            .Where(static operation => operation.Name.Length > 0)
            .ToArray();
        var evidence = new List<PdfTrustMigrationEvidence>();

        AddDetected(
            evidence,
            invocations,
            PdfTrustCapability.DigitalSignature,
            "CANMIGNUTNET021",
            PdfTrustSourceSemantics.CryptographicSignature,
            static name => ContainsAny(name, "Sign", "Certificate", "ApplySignature"),
            "IPdfTrustPostProcessingPipeline.ProcessAsync",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["credentialHandling"] = "runtime-only",
                ["automaticReplacement"] = "false"
            });
        AddDetected(
            evidence,
            invocations,
            PdfTrustCapability.Timestamp,
            "CANMIGNUTNET021",
            PdfTrustSourceSemantics.CryptographicSignature,
            static name => ContainsAny(name, "Timestamp"),
            "PdfTrustPipelineContext.DocumentTimestampAuthority",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["credentialHandling"] = "runtime-only",
                ["automaticReplacement"] = "false"
            });
        AddDetected(
            evidence,
            invocations,
            PdfTrustCapability.SignatureValidation,
            "CANMIGNUTNET021",
            PdfTrustSourceSemantics.ComplianceValidation,
            static name => ContainsAny(name, "VerifySignature", "ValidateSignature"),
            "IPdfTrustInspector.InspectAsync");
        AddDetected(
            evidence,
            invocations,
            PdfTrustCapability.Encryption,
            "CANMIGNUTNET023",
            PdfTrustSourceSemantics.PasswordEncryption,
            static name => ContainsAny(name, "Encrypt", "Security", "Password"),
            "PdfSaveOptions.Encryption");

        var redactionOperations = invocations
            .Where(static operation => ContainsAny(operation.Name, "Redact", "Redaction"))
            .OrderBy(static operation => operation.Name, StringComparer.Ordinal)
            .ToArray();
        if (redactionOperations.Length > 0)
        {
            var appliesRedaction = redactionOperations.Any(static name =>
                name.Name.Contains("Apply", StringComparison.OrdinalIgnoreCase));
            var semantics = appliesRedaction
                ? PdfTrustSourceSemantics.ContentRemovingRedaction
                : PdfTrustSourceSemantics.VisualRedactionOnly;
            evidence.Add(Create(
                PdfTrustCapability.SecureRedaction,
                PdfTrustMigrationPolicy.ResolveDisposition(
                    semantics,
                    targetHasEquivalentSemantics: appliesRedaction,
                    providerOptionsAreComplete: false,
                    targetCanValidateEndToEnd: true),
                string.Join(", ", redactionOperations.Select(static operation => operation.Name)
                    .Distinct(StringComparer.Ordinal)),
                "IPdfRedactionApplier.ApplyAsync",
                semantics,
                "CANMIGNUTNET022",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sourceGuarantee"] = appliesRedaction ? "content-removal-requested" : "visual-mark-only",
                    ["automaticReplacement"] = "false"
                },
                redactionOperations.Select(static operation => operation.Location).ToArray()));
        }

        var pdfAOperations = invocations
            .Where(static operation => operation.Name == "ConvertToPDFA" ||
                operation.Arguments.Contains("PDF_A", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (pdfAOperations.Length > 0)
        {
            var profile = pdfAOperations.Any(static operation =>
                operation.Arguments.Contains("PDF_A_2b", StringComparison.OrdinalIgnoreCase))
                    ? "PDF/A-2b"
                    : "unknown";
            evidence.Add(Create(
                PdfTrustCapability.PdfA,
                PdfTrustMigrationDisposition.ReviewRequired,
                string.Join(", ", pdfAOperations.Select(static operation => operation.Name).Distinct(StringComparer.Ordinal)),
                profile == "PDF/A-2b" ? "PdfSaveOptions.ComplianceProfile = PdfA2B" : null,
                PdfTrustSourceSemantics.ComplianceConversion,
                profile == "PDF/A-2b" ? "CANMIGNUTNET032" : "CANMIGNUTNET006",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["profile"] = profile },
                pdfAOperations.Select(static operation => operation.Location).ToArray()));
        }

        AddDetected(
            evidence,
            invocations,
            PdfTrustCapability.PdfUa,
            "CANMIGNUTNET023",
            PdfTrustSourceSemantics.ComplianceValidation,
            static name => ContainsAny(name, "PdfUA", "Accessibility", "Tagged"),
            "PdfSaveOptions.PdfUa");

        return new PdfTrustMigrationReport
        {
            ProviderKey = ProviderKey,
            MigrationKind = "code",
            Evidence = evidence
        };
    }

    private static void AddDetected(
        ICollection<PdfTrustMigrationEvidence> evidence,
        IEnumerable<DetectedOperation> invocations,
        PdfTrustCapability capability,
        string diagnosticId,
        PdfTrustSourceSemantics semantics,
        Func<string, bool> matches,
        string targetOperation,
        IReadOnlyDictionary<string, string>? providerOptions = null)
    {
        var operations = invocations
            .Where(operation => matches(operation.Name))
            .OrderBy(static operation => operation.Name, StringComparer.Ordinal)
            .ToArray();
        if (operations.Length == 0)
            return;

        evidence.Add(Create(
            capability,
            PdfTrustMigrationDisposition.ReviewRequired,
            string.Join(", ", operations.Select(static operation => operation.Name)
                .Distinct(StringComparer.Ordinal)),
            targetOperation,
            semantics,
            diagnosticId,
            providerOptions,
            operations.Select(static operation => operation.Location).ToArray()));
    }

    private static PdfTrustMigrationEvidence Create(
        PdfTrustCapability capability,
        PdfTrustMigrationDisposition disposition,
        string sourceOperation,
        string? targetOperation,
        PdfTrustSourceSemantics sourceSemantics,
        string diagnosticId,
        IReadOnlyDictionary<string, string>? providerOptions = null,
        IReadOnlyList<MigrationSourceLocation>? sourceLocations = null) => new()
    {
        Capability = capability,
        Disposition = disposition,
        SourceOperation = sourceOperation,
        TargetOperation = targetOperation,
        SourceSemantics = sourceSemantics,
        DiagnosticId = diagnosticId,
        DocumentationPath = DocumentationFor(capability),
        SourceLocations = sourceLocations ?? [],
        ProviderOptions = providerOptions ?? new Dictionary<string, string>(StringComparer.Ordinal)
    };

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

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

    private static MigrationSourceLocation SourceLocation(InvocationExpressionSyntax invocation)
    {
        var start = invocation.GetLocation().GetLineSpan().StartLinePosition;
        return new MigrationSourceLocation(start.Line + 1, start.Character + 1);
    }

    private sealed record DetectedOperation(
        string Name,
        string Arguments,
        MigrationSourceLocation Location);
}
