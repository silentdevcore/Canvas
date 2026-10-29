namespace PVAD.WebApi.Services;

public sealed record MigrationResult(
    string PvadCode,
    IReadOnlyList<MigrationDiagnostic> Diagnostics,
    MigrationSummary Summary)
{
    [Obsolete("Use PvadCode. The compatibility alias is retained for 2.x and scheduled for removal in 3.0.0.")]
    public string PxaCode => PvadCode;

    public PVAD.Migration.Abstractions.PdfTrustMigrationReport? PdfTrust { get; init; }
}

public sealed record MigrationSummary(
    int ConvertedCount,
    int WarningCount,
    int ErrorCount,
    int TotalDiagnostics);
