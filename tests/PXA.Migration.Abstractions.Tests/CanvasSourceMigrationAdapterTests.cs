using CanvasDiagnostic = Canvas.Migration.Abstractions.MigrationDiagnostic;
using CanvasDiagnosticSeverity = Canvas.Migration.Abstractions.MigrationDiagnosticSeverity;
using CanvasResult = Canvas.Migration.Abstractions.MigrationResult;
using CanvasSourceMigration = Canvas.Migration.Abstractions.ISourceMigration;

namespace PXA.Migration.Abstractions.Tests;

public sealed class CanvasSourceMigrationAdapterTests
{
    [Fact]
    public void Migrate_MapsCanvasResultToPxaResult()
    {
        var migration = new FakeCanvasMigration();
        var adapter = new CanvasSourceMigrationAdapter(migration);

        var result = adapter.Migrate("old code");

        Assert.Equal("new code: old code", result.MigratedCode);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CANMIG999", diagnostic.Id);
        Assert.Equal("Mapped diagnostic", diagnostic.Message);
        Assert.Equal(MigrationDiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void AsPxaMigration_WrapsCanvasMigration()
    {
        CanvasSourceMigration migration = new FakeCanvasMigration();

        var result = migration.AsPxaMigration().Migrate("source");

        Assert.Equal("new code: source", result.MigratedCode);
    }

    private sealed class FakeCanvasMigration : CanvasSourceMigration
    {
        public CanvasResult Migrate(string sourceCode)
        {
            return new CanvasResult
            {
                MigratedCode = $"new code: {sourceCode}",
                Diagnostics =
                [
                    new CanvasDiagnostic
                    {
                        Id = "CANMIG999",
                        Message = "Mapped diagnostic",
                        Severity = CanvasDiagnosticSeverity.Warning,
                    },
                ],
            };
        }
    }
}
