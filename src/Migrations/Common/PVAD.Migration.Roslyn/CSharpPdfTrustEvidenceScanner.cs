using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Roslyn;

public static class CSharpPdfTrustEvidenceScanner
{
    public static PdfTrustMigrationReport Scan(string sourceCode, PdfTrustProviderProfile profile)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentNullException.ThrowIfNull(profile);

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var identifiers = root.DescendantNodes()
            .SelectMany(static node => node switch
            {
                InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } =>
                    [member.Name.Identifier.ValueText],
                ObjectCreationExpressionSyntax creation => [SimpleName(creation.Type)],
                IdentifierNameSyntax identifier => [identifier.Identifier.ValueText],
                _ => Array.Empty<string>()
            })
            .Where(static value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var evidence = profile.Capabilities
            .Select(capability => CreateEvidence(capability, identifiers))
            .Where(static item => item is not null)
            .Select(static item => item!)
            .ToList();

        AddUnsupportedProfileEvidence(evidence, identifiers, profile);
        return new PdfTrustMigrationReport
        {
            ProviderKey = profile.ProviderKey,
            MigrationKind = profile.MigrationKind,
            Evidence = evidence
        };
    }

    private static PdfTrustMigrationEvidence? CreateEvidence(
        PdfTrustProviderCapability capability,
        IReadOnlySet<string> identifiers)
    {
        var detected = identifiers
            .Where(identifier => capability.DetectedApiMarkers.Any(marker =>
                identifier.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (detected.Length == 0)
            return null;

        var semantics = ResolveSemantics(capability.Capability, detected);
        var disposition = PdfTrustMigrationPolicy.ResolveDisposition(
            semantics,
            targetHasEquivalentSemantics: semantics is not (
                PdfTrustSourceSemantics.SignatureAppearanceOnly or PdfTrustSourceSemantics.VisualRedactionOnly),
            providerOptionsAreComplete: false,
            targetCanValidateEndToEnd: true);
        return new PdfTrustMigrationEvidence
        {
            Capability = capability.Capability,
            Disposition = disposition,
            SourceOperation = string.Join(", ", detected),
            TargetOperation = TargetOperation(capability.Capability),
            SourceSemantics = semantics,
            DiagnosticId = $"CANMIGPDFTRUST{100 + (int)capability.Capability:D3}",
            DocumentationPath = capability.DocumentationPath,
            ProviderOptions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["detectedApiMarkers"] = string.Join(",", detected),
                ["providerOptionsComplete"] = "false"
            }
        };
    }

    private static void AddUnsupportedProfileEvidence(
        List<PdfTrustMigrationEvidence> evidence,
        IReadOnlySet<string> identifiers,
        PdfTrustProviderProfile profile)
    {
        foreach (var unsupportedProfile in profile.UnsupportedTargetProfiles)
        {
            var normalized = unsupportedProfile.Replace("/", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);
            if (!identifiers.Any(identifier =>
                    identifier.Replace("_", string.Empty, StringComparison.Ordinal)
                        .Contains(normalized, StringComparison.OrdinalIgnoreCase)))
                continue;

            var capability = unsupportedProfile.StartsWith("PDF/A", StringComparison.Ordinal)
                ? PdfTrustCapability.PdfA
                : PdfTrustCapability.PdfUa;
            evidence.RemoveAll(item => item.Capability == capability);
            evidence.Add(new PdfTrustMigrationEvidence
            {
                Capability = capability,
                Disposition = PdfTrustMigrationDisposition.Unsupported,
                SourceOperation = unsupportedProfile,
                SourceSemantics = PdfTrustSourceSemantics.ComplianceConversion,
                DiagnosticId = capability == PdfTrustCapability.PdfA
                    ? "CANMIGPDFTRUST180"
                    : "CANMIGPDFTRUST181",
                DocumentationPath = profile.Capabilities.Single(item => item.Capability == capability).DocumentationPath,
                ProviderOptions = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["profile"] = unsupportedProfile,
                    ["targetValidationAvailable"] = "false"
                }
            });
        }
    }

    private static PdfTrustSourceSemantics ResolveSemantics(
        PdfTrustCapability capability,
        IReadOnlyList<string> detected) => capability switch
    {
        PdfTrustCapability.DigitalSignature when detected.All(static marker =>
            marker.Contains("Appearance", StringComparison.OrdinalIgnoreCase) ||
            marker.Contains("Field", StringComparison.OrdinalIgnoreCase)) =>
            PdfTrustSourceSemantics.SignatureAppearanceOnly,
        PdfTrustCapability.DigitalSignature or PdfTrustCapability.Timestamp =>
            PdfTrustSourceSemantics.CryptographicSignature,
        PdfTrustCapability.SignatureValidation or PdfTrustCapability.PdfUa =>
            PdfTrustSourceSemantics.ComplianceValidation,
        PdfTrustCapability.Encryption when detected.Any(static marker =>
            marker.Contains("Certificate", StringComparison.OrdinalIgnoreCase)) =>
            PdfTrustSourceSemantics.CertificateEncryption,
        PdfTrustCapability.Encryption => PdfTrustSourceSemantics.PasswordEncryption,
        PdfTrustCapability.SecureRedaction when detected.All(static marker =>
            marker.Contains("Annotation", StringComparison.OrdinalIgnoreCase) ||
            marker.Contains("Area", StringComparison.OrdinalIgnoreCase)) =>
            PdfTrustSourceSemantics.VisualRedactionOnly,
        PdfTrustCapability.SecureRedaction => PdfTrustSourceSemantics.ContentRemovingRedaction,
        PdfTrustCapability.PdfA => PdfTrustSourceSemantics.ComplianceConversion,
        _ => PdfTrustSourceSemantics.Unknown
    };

    private static string? TargetOperation(PdfTrustCapability capability) => capability switch
    {
        PdfTrustCapability.DigitalSignature => "PdfTrustPostProcessingPipeline.Signing",
        PdfTrustCapability.Timestamp => "PdfTrustPipelineContext.DocumentTimestampAuthority",
        PdfTrustCapability.SignatureValidation => "IPdfTrustInspector.InspectAsync",
        PdfTrustCapability.Encryption => "PdfSaveOptions.Encryption",
        PdfTrustCapability.SecureRedaction => "IPdfRedactionApplier.ApplyAsync",
        PdfTrustCapability.PdfA => "PdfSaveOptions.ComplianceProfile",
        PdfTrustCapability.PdfUa => "PdfSaveOptions.PdfUa",
        _ => null
    };

    private static string SimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => type.ToString().Split('.').Last()
    };
}
