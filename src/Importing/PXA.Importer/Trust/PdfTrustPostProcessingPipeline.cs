using PXA.Importer.Redaction;
using PXA.Importer.Signing;

namespace PXA.Importer.Trust;

public enum PdfTrustPipelineStep
{
    Redaction,
    Signing,
    Validation
}

public sealed record PdfTrustRedactionOptions
{
    public required IReadOnlyList<PdfRedactionRegion> Regions { get; init; }
    public PdfRedactionSanitizationPolicy Sanitization { get; init; } = new();
    public PdfRedactionPreflightOptions Preflight { get; init; } = new();
    public string? Reason { get; init; }
    public string? Actor { get; init; }
}

public sealed record PdfTrustPipelineValidationPolicy
{
    public bool RequireValidStructure { get; init; } = true;
    public bool RequirePdfA { get; init; }
    public bool RequirePdfUa { get; init; }
    public bool RequireValidSignatures { get; init; }
    public bool RejectRiskyContent { get; init; }
    public PdfTrustInspectionOptions Inspection { get; init; } = new();
}

/// <summary>
/// Serializable signing policy. Keys, timestamp services, and long-term validation material are
/// deliberately supplied only through <see cref="PdfTrustPipelineContext"/>.
/// </summary>
public sealed record PdfTrustSigningOptions
{
    public PdfSigningCertificateSource CertificateSource { get; init; } = new();
    public string FieldName { get; init; } = "Signature1";
    public string? Reason { get; init; }
    public string? Location { get; init; }
    public string? ContactInfo { get; init; }
    public DateTimeOffset? SigningTime { get; init; }
    public PdfSignatureDigestAlgorithm DigestAlgorithm { get; init; } = PdfSignatureDigestAlgorithm.Sha256;
    public PdfPadesProfile Profile { get; init; } = PdfPadesProfile.BaselineB;
    public PdfTimestampPolicy Timestamp { get; init; } = new();
    public PdfRevocationDataPolicy? RevocationData { get; init; }
    public PdfPostSignValidationPolicy Validation { get; init; } = new();
    public PdfSignatureAppearanceOptions? Appearance { get; init; }
    public int ContentsPlaceholderBytes { get; init; } = 32 * 1024;
    public PdfCertificationLevel Certification { get; init; } = PdfCertificationLevel.None;
    public PdfSignatureFieldLock? FieldLock { get; init; }
}

/// <summary>
/// Describes final-byte trust operations. It contains policy and stable key references only;
/// cryptographic credentials are supplied separately through <see cref="PdfTrustPipelineContext"/>.
/// </summary>
public sealed record PdfTrustPostProcessingOptions
{
    public PdfTrustRedactionOptions? Redaction { get; init; }
    public PdfTrustSigningOptions? Signing { get; init; }
    public PdfTrustPipelineValidationPolicy Validation { get; init; } = new();
}

/// <summary>
/// Runtime-only dependencies resolved by the trusted host. This context must never be serialized
/// into a design, workspace, job payload, diagnostic, or audit event.
/// </summary>
public sealed record PdfTrustPipelineContext
{
    public IPdfSigningKey? DocumentSigningKey { get; init; }
    public IPdfTimestampAuthority? DocumentTimestampAuthority { get; init; }
    public PdfLongTermValidationMaterial? LongTermValidationMaterial { get; init; }
    public IPdfLongTermValidationMaterialProvider? LongTermValidationProvider { get; init; }
    public IPdfSigningKey? RedactionAuditSigningKey { get; init; }
    public IPdfTimestampAuthority? RedactionAuditTimestampAuthority { get; init; }
}

public sealed record PdfTrustPostProcessingResult(
    byte[] Bytes,
    IReadOnlyList<PdfTrustPipelineStep> CompletedSteps,
    PdfTrustReport TrustReport,
    PdfRedactionApplyResult? Redaction = null,
    PdfSigningResult? Signing = null);

public sealed class PdfTrustPipelineValidationException(
    string code,
    string message,
    PdfTrustReport? report = null) : InvalidOperationException($"{code}: {message}")
{
    public string Code { get; } = code;
    public PdfTrustReport? Report { get; } = report;
}

public interface IPdfTrustPostProcessingPipeline
{
    Task<PdfTrustPostProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> source,
        PdfTrustPostProcessingOptions? options = null,
        PdfTrustPipelineContext? context = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies destructive changes before signatures and validates the exact final bytes returned to
/// the caller. The pipeline never resolves or stores credentials itself.
/// </summary>
public sealed class PdfTrustPostProcessingPipeline(
    IPdfRedactionApplier redactionApplier,
    IPdfPadesSigningService signingService,
    IPdfTrustInspector trustInspector) : IPdfTrustPostProcessingPipeline
{
    public async Task<PdfTrustPostProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> source,
        PdfTrustPostProcessingOptions? options = null,
        PdfTrustPipelineContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (source.IsEmpty)
            throw new ArgumentException("A non-empty PDF is required.", nameof(source));

        options ??= new PdfTrustPostProcessingOptions();
        context ??= new PdfTrustPipelineContext();
        ValidatePlan(options, context);

        var bytes = source.ToArray();
        var steps = new List<PdfTrustPipelineStep>();
        PdfRedactionApplyResult? redactionResult = null;
        PdfSigningResult? signingResult = null;

        if (options.Redaction is { } redaction)
        {
            await using var stream = new MemoryStream(bytes, writable: false);
            redactionResult = await redactionApplier.ApplyAsync(
                stream,
                redaction.Regions,
                new PdfRedactionApplyOptions
                {
                    Sanitization = redaction.Sanitization,
                    Preflight = redaction.Preflight,
                    Reason = redaction.Reason,
                    Actor = redaction.Actor,
                    AuditSigningKey = context.RedactionAuditSigningKey,
                    AuditTimestampAuthority = context.RedactionAuditTimestampAuthority
                },
                cancellationToken).ConfigureAwait(false);
            if (!redactionResult.Verification.Passed)
                throw new PdfTrustPipelineValidationException(
                    "PXAPDFPIPELINE102",
                    "Redaction verification failed, so the result cannot continue to signing.");
            bytes = redactionResult.Bytes;
            steps.Add(PdfTrustPipelineStep.Redaction);
        }

        if (options.Signing is { } signing)
        {
            var effectiveSigning = new PdfSigningOptions
            {
                CertificateSource = signing.CertificateSource,
                FieldName = signing.FieldName,
                Reason = signing.Reason,
                Location = signing.Location,
                ContactInfo = signing.ContactInfo,
                SigningTime = signing.SigningTime,
                DigestAlgorithm = signing.DigestAlgorithm,
                Profile = signing.Profile,
                Timestamp = signing.Timestamp,
                RevocationData = signing.RevocationData,
                Validation = signing.Validation,
                Appearance = signing.Appearance,
                ContentsPlaceholderBytes = signing.ContentsPlaceholderBytes,
                LongTermValidation = context.LongTermValidationMaterial,
                LongTermValidationProvider = context.LongTermValidationProvider,
                Certification = signing.Certification,
                FieldLock = signing.FieldLock
            };
            signingResult = await signingService.SignAsync(
                bytes,
                context.DocumentSigningKey!,
                effectiveSigning,
                context.DocumentTimestampAuthority,
                cancellationToken).ConfigureAwait(false);
            bytes = signingResult.Bytes;
            steps.Add(PdfTrustPipelineStep.Signing);
        }

        await using var validationStream = new MemoryStream(bytes, writable: false);
        var report = await trustInspector
            .InspectAsync(validationStream, options.Validation.Inspection, cancellationToken)
            .ConfigureAwait(false);
        EnforceValidation(options.Validation, report);
        steps.Add(PdfTrustPipelineStep.Validation);

        return new PdfTrustPostProcessingResult(bytes, steps, report, redactionResult, signingResult);
    }

    private static void ValidatePlan(PdfTrustPostProcessingOptions options, PdfTrustPipelineContext context)
    {
        if (options.Redaction is { Regions.Count: 0 })
            throw new PdfTrustPipelineValidationException("PXAPDFPIPELINE100", "At least one redaction region is required.");

        if (options.Signing is not { } signing)
            return;
        if (context.DocumentSigningKey is null)
            throw new PdfTrustPipelineValidationException("PXAPDFPIPELINE101", "A runtime document signing key is required.");
        if (signing.CertificateSource.Kind == PdfSigningCertificateSourceKind.OrganizationKeyProvider &&
            (string.IsNullOrWhiteSpace(signing.CertificateSource.ProviderId) ||
             string.IsNullOrWhiteSpace(signing.CertificateSource.KeyId)))
        {
            throw new PdfTrustPipelineValidationException(
                "PXAPDFPIPELINE103",
                "Organization signing requires stable provider and key identifiers.");
        }

        var requiresTimestamp = signing.Profile is not PdfPadesProfile.BaselineB;
        if (signing.Timestamp.Required is { } timestampRequired && timestampRequired != requiresTimestamp)
        {
            throw new PdfTrustPipelineValidationException(
                "PXAPDFPIPELINE104",
                "The timestamp policy is incompatible with the selected PAdES profile.");
        }
        if (requiresTimestamp && context.DocumentTimestampAuthority is null)
        {
            throw new PdfTrustPipelineValidationException(
                "PXAPDFPIPELINE105",
                "PAdES Baseline T, LT, and LTA require a runtime timestamp authority.");
        }

        var requiresLongTermValidation = signing.Profile is PdfPadesProfile.BaselineLT or PdfPadesProfile.BaselineLTA;
        var hasLongTermValidationMaterial = context.LongTermValidationMaterial is not null;
        var hasLongTermValidationProvider = context.LongTermValidationProvider is not null;
        if (requiresLongTermValidation && hasLongTermValidationMaterial == hasLongTermValidationProvider)
        {
            throw new PdfTrustPipelineValidationException(
                "PXAPDFPIPELINE106",
                "PAdES Baseline LT and LTA require exactly one runtime long-term validation source.");
        }
        if (!requiresLongTermValidation && (hasLongTermValidationMaterial || hasLongTermValidationProvider))
        {
            throw new PdfTrustPipelineValidationException(
                "PXAPDFPIPELINE106",
                "Runtime long-term validation material is valid only for PAdES Baseline LT and LTA.");
        }

        if (signing.RevocationData is { } revocationData &&
            (revocationData == PdfRevocationDataPolicy.EmbedForLongTermValidation) != requiresLongTermValidation)
        {
            throw new PdfTrustPipelineValidationException(
                "PXAPDFPIPELINE107",
                "The revocation-data policy is incompatible with the selected PAdES profile.");
        }
    }

    private static void EnforceValidation(PdfTrustPipelineValidationPolicy policy, PdfTrustReport report)
    {
        if (policy.RequireValidStructure && report.StructureStatus != PdfTrustStatus.Valid)
            Reject("PXAPDFPIPELINE200", "The final PDF structure is not valid.", report);
        if (policy.RequirePdfA && report.PdfA.Status != PdfTrustStatus.Valid)
            Reject("PXAPDFPIPELINE201", "The final PDF does not have a valid PDF/A claim.", report);
        if (policy.RequirePdfUa && report.PdfUa.Status != PdfTrustStatus.Valid)
            Reject("PXAPDFPIPELINE202", "The final PDF does not have a valid PDF/UA claim.", report);
        if (policy.RequireValidSignatures &&
            (report.Signatures.Count == 0 || report.Signatures.Any(signature => signature.Status != PdfTrustStatus.Valid)))
            Reject("PXAPDFPIPELINE203", "The final PDF does not contain only valid signatures.", report);
        if (policy.RejectRiskyContent && report.Risk?.HasRiskyContent == true)
            Reject("PXAPDFPIPELINE204", "The final PDF contains active or embedded risky content.", report);
    }

    private static void Reject(string code, string message, PdfTrustReport report) =>
        throw new PdfTrustPipelineValidationException(code, message, report);
}
