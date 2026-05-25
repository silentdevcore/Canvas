namespace Canvas.WebApi.Services;

public sealed record MigrationResult(string CanvasCode, IReadOnlyList<MigrationDiagnostic> Diagnostics);
