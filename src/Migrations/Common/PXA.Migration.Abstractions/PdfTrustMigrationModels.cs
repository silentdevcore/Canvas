namespace PXA.Migration.Abstractions;

public enum PdfTrustCapability
{
    DigitalSignature,
    Timestamp,
    SignatureValidation,
    Encryption,
    SecureRedaction,
    PdfA,
    PdfUa
}

public enum PdfTrustMigrationDisposition
{
    Automatic,
    ReviewRequired,
    Unsupported
}

public enum PdfTrustSourceSemantics
{
    Unknown,
    CryptographicSignature,
    SignatureAppearanceOnly,
    ContentRemovingRedaction,
    VisualRedactionOnly,
    PasswordEncryption,
    CertificateEncryption,
    ComplianceConversion,
    ComplianceValidation
}

public sealed record MigrationSourceLocation(int Line, int Column);

public sealed record PdfTrustMigrationEvidence
{
    public required PdfTrustCapability Capability { get; init; }
    public required PdfTrustMigrationDisposition Disposition { get; init; }
    public required string SourceOperation { get; init; }
    public string? TargetOperation { get; init; }
    public PdfTrustSourceSemantics SourceSemantics { get; init; } = PdfTrustSourceSemantics.Unknown;
    public required string DiagnosticId { get; init; }
    public required string DocumentationPath { get; init; }
    public IReadOnlyList<MigrationSourceLocation> SourceLocations { get; init; } = [];
    public IReadOnlyDictionary<string, string> ProviderOptions { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record PdfTrustMigrationReport
{
    public required string ProviderKey { get; init; }
    public required string MigrationKind { get; init; }
    public IReadOnlyList<PdfTrustMigrationEvidence> Evidence { get; init; } = [];

    public IReadOnlyList<PdfTrustMigrationManualAction> ManualActions => Evidence
        .Where(static item => item.Disposition != PdfTrustMigrationDisposition.Automatic)
        .Select(static item => new PdfTrustMigrationManualAction
        {
            ActionId = item.DiagnosticId,
            Capability = item.Capability,
            Disposition = item.Disposition,
            SourceOperation = item.SourceOperation,
            TargetOperation = item.TargetOperation,
            DocumentationPath = item.DocumentationPath
        })
        .ToArray();

    public PdfTrustMigrationDisposition OverallDisposition => Evidence.Any(static item =>
        item.Disposition == PdfTrustMigrationDisposition.Unsupported)
            ? PdfTrustMigrationDisposition.Unsupported
            : Evidence.Any(static item => item.Disposition == PdfTrustMigrationDisposition.ReviewRequired)
                ? PdfTrustMigrationDisposition.ReviewRequired
                : PdfTrustMigrationDisposition.Automatic;
}

public sealed record PdfTrustMigrationManualAction
{
    public required string ActionId { get; init; }
    public required PdfTrustCapability Capability { get; init; }
    public required PdfTrustMigrationDisposition Disposition { get; init; }
    public required string SourceOperation { get; init; }
    public string? TargetOperation { get; init; }
    public required string DocumentationPath { get; init; }
}

public sealed record PdfTrustProviderCapability
{
    public required PdfTrustCapability Capability { get; init; }
    public required PdfTrustMigrationDisposition DefaultDisposition { get; init; }
    public IReadOnlyList<string> DetectedApiMarkers { get; init; } = [];
    public required string DocumentationPath { get; init; }
}

public sealed record PdfTrustProviderProfile
{
    public required string ProviderKey { get; init; }
    public required string DisplayName { get; init; }
    public required string MigrationKind { get; init; }
    public required IReadOnlyList<PdfTrustProviderCapability> Capabilities { get; init; }
    public IReadOnlyList<string> UnsupportedTargetProfiles { get; init; } = ["PDF/A-4", "PDF/UA-2"];
}

public static class PdfTrustMigrationPolicy
{
    public static PdfTrustMigrationDisposition ResolveDisposition(
        PdfTrustSourceSemantics sourceSemantics,
        bool targetHasEquivalentSemantics,
        bool providerOptionsAreComplete,
        bool targetCanValidateEndToEnd)
    {
        if (sourceSemantics is PdfTrustSourceSemantics.SignatureAppearanceOnly or
            PdfTrustSourceSemantics.VisualRedactionOnly)
        {
            return PdfTrustMigrationDisposition.Unsupported;
        }

        return targetHasEquivalentSemantics && providerOptionsAreComplete && targetCanValidateEndToEnd
            ? PdfTrustMigrationDisposition.Automatic
            : PdfTrustMigrationDisposition.ReviewRequired;
    }
}
