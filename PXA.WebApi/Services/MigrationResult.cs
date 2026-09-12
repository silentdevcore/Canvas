namespace PXA.WebApi.Services;

public sealed record MigrationResult(
    string PxaCode,
    IReadOnlyList<MigrationDiagnostic> Diagnostics,
    MigrationSummary Summary)
{
    public PXA.Migration.Abstractions.PdfTrustMigrationReport? PdfTrust { get; init; }
}

public sealed record MigrationSummary(
    int ConvertedCount,
    int WarningCount,
    int ErrorCount,
    int TotalDiagnostics);
