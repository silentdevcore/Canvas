namespace Canvas.WebApi.Services;

public sealed record MigrationResult(
    string CanvasCode,
    IReadOnlyList<MigrationDiagnostic> Diagnostics,
    MigrationSummary Summary);

public sealed record MigrationSummary(
    int ConvertedCount,
    int WarningCount,
    int ErrorCount,
    int TotalDiagnostics);
