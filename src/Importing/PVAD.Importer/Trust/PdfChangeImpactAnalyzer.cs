namespace PVAD.Importer.Trust;

public enum PdfSaveStrategy
{
    IncrementalUpdate,
    FullRewrite
}

public enum PdfMutationKind
{
    AddSignature,
    FillForm,
    AddAnnotation,
    ModifyPageContent,
    ModifyMetadata,
    ApplyRedaction,
    ComplianceConversion
}

public enum PdfChangeImpactStatus
{
    Preserved,
    Invalidated,
    RequiresValidation,
    NotApplicable
}

public sealed record PdfSignatureChangeImpact(
    int ObjectNumber,
    int Generation,
    PdfChangeImpactStatus Status,
    string Reason);

public sealed record PdfComplianceChangeImpact(
    string Standard,
    string? Profile,
    PdfChangeImpactStatus Status,
    string Reason);

public sealed record PdfChangeImpactReport(
    PdfSaveStrategy SaveStrategy,
    PdfMutationKind Mutation,
    IReadOnlyList<PdfSignatureChangeImpact> Signatures,
    IReadOnlyList<PdfComplianceChangeImpact> ComplianceClaims,
    bool RequiresExplicitConfirmation,
    IReadOnlyList<PdfTrustDiagnostic> Diagnostics);

public sealed class PdfChangeImpactAnalyzer
{
    public PdfChangeImpactReport Analyze(
        PdfTrustReport trustReport,
        PdfSaveStrategy saveStrategy,
        PdfMutationKind mutation)
    {
        ArgumentNullException.ThrowIfNull(trustReport);

        var signatureImpacts = trustReport.Signatures
            .Select(signature => AnalyzeSignature(signature, saveStrategy, mutation))
            .ToArray();
        var complianceImpacts = new[] { trustReport.PdfA, trustReport.PdfUa }
            .Where(static claim => claim.IsClaimed)
            .Select(claim => AnalyzeCompliance(claim, mutation))
            .ToArray();
        var requiresConfirmation = signatureImpacts.Any(static impact => impact.Status is
            PdfChangeImpactStatus.Invalidated or PdfChangeImpactStatus.RequiresValidation);

        var diagnostics = new List<PdfTrustDiagnostic>();
        if (saveStrategy == PdfSaveStrategy.FullRewrite && signatureImpacts.Length > 0)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST501",
                PdfTrustDiagnosticSeverity.Error,
                "A full rewrite changes signed byte ranges and invalidates existing PDF signatures."));
        }
        else if (saveStrategy == PdfSaveStrategy.IncrementalUpdate && signatureImpacts.Length > 0)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST502",
                PdfTrustDiagnosticSeverity.Warning,
                "An incremental update preserves earlier signed bytes, but certification permissions and post-signature changes still require validation."));
        }

        if (complianceImpacts.Length > 0)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST503",
                PdfTrustDiagnosticSeverity.Warning,
                "Declared compliance profiles must be validated again after the planned mutation."));
        }

        return new PdfChangeImpactReport(
            saveStrategy,
            mutation,
            signatureImpacts,
            complianceImpacts,
            requiresConfirmation,
            diagnostics);
    }

    private static PdfSignatureChangeImpact AnalyzeSignature(
        PdfSignatureInspection signature,
        PdfSaveStrategy saveStrategy,
        PdfMutationKind mutation)
    {
        if (saveStrategy == PdfSaveStrategy.FullRewrite)
        {
            return new PdfSignatureChangeImpact(
                signature.ObjectNumber,
                signature.Generation,
                PdfChangeImpactStatus.Invalidated,
                "A full rewrite changes the byte sequence covered by the existing signature.");
        }

        var reason = mutation == PdfMutationKind.AddSignature
            ? "The previous signed revision remains byte-for-byte intact, but the complete signature chain must be validated after appending the new signature."
            : "The previous signed revision remains byte-for-byte intact, but certification permissions and the semantic effect of the appended change are not yet validated.";
        return new PdfSignatureChangeImpact(
            signature.ObjectNumber,
            signature.Generation,
            PdfChangeImpactStatus.RequiresValidation,
            reason);
    }

    private static PdfComplianceChangeImpact AnalyzeCompliance(
        PdfComplianceClaim claim,
        PdfMutationKind mutation)
    {
        var reason = mutation == PdfMutationKind.ComplianceConversion
            ? "Conversion output requires independent validation before the requested profile can be claimed."
            : "Any document mutation can affect conformance and requires profile validation of the saved output.";
        return new PdfComplianceChangeImpact(
            claim.Standard,
            claim.Profile,
            PdfChangeImpactStatus.RequiresValidation,
            reason);
    }
}
