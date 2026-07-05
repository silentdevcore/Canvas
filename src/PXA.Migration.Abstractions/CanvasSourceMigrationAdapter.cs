using CanvasDiagnostic = Canvas.Migration.Abstractions.MigrationDiagnostic;
using CanvasDiagnosticSeverity = Canvas.Migration.Abstractions.MigrationDiagnosticSeverity;
using CanvasResult = Canvas.Migration.Abstractions.MigrationResult;
using CanvasSourceMigration = Canvas.Migration.Abstractions.ISourceMigration;

namespace PXA.Migration.Abstractions;

public sealed class CanvasSourceMigrationAdapter : ISourceMigration
{
    private readonly CanvasSourceMigration inner;

    public CanvasSourceMigrationAdapter(CanvasSourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return ToPxaResult(inner.Migrate(sourceCode));
    }

    private static MigrationResult ToPxaResult(CanvasResult result)
    {
        return new MigrationResult
        {
            MigratedCode = result.MigratedCode,
            Diagnostics = result.Diagnostics.Select(ToPxaDiagnostic).ToArray(),
        };
    }

    private static MigrationDiagnostic ToPxaDiagnostic(CanvasDiagnostic diagnostic)
    {
        return new MigrationDiagnostic
        {
            Id = diagnostic.Id,
            Message = diagnostic.Message,
            Severity = ToPxaSeverity(diagnostic.Severity),
        };
    }

    private static MigrationDiagnosticSeverity ToPxaSeverity(CanvasDiagnosticSeverity severity)
    {
        return severity switch
        {
            CanvasDiagnosticSeverity.Warning => MigrationDiagnosticSeverity.Warning,
            CanvasDiagnosticSeverity.Error => MigrationDiagnosticSeverity.Error,
            _ => MigrationDiagnosticSeverity.Info,
        };
    }
}
