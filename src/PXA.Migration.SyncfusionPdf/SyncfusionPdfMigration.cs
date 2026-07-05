using PXA.Migration.Abstractions;
using CanvasSyncfusionPdfMigration = Canvas.Migration.SyncfusionPdf.SyncfusionPdfMigration;

namespace PXA.Migration.SyncfusionPdf;

public sealed class SyncfusionPdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public SyncfusionPdfMigration()
        : this(new CanvasSyncfusionPdfMigration().AsPxaMigration())
    {
    }

    internal SyncfusionPdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
