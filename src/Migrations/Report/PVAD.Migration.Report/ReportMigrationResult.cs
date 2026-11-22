using PVAD.Core.Contracts;
using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Report;

/// <summary>Holds a report's converted design and review diagnostics without rendering it.</summary>
public sealed class ReportMigrationResult
{
    /// <summary>Gets the converted editable Designer document.</summary>
    public required DesignExportDto Design { get; init; }

    /// <summary>Gets unsupported, approximated and informational migration findings.</summary>
    public IReadOnlyList<MigrationDiagnostic> Diagnostics { get; init; } = Array.Empty<MigrationDiagnostic>();
}
