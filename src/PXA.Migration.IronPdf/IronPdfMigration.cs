using PXA.Migration.Abstractions;
using CanvasIronPdfMigration = Canvas.Migration.IronPdf.IronPdfMigration;

namespace PXA.Migration.IronPdf;

public sealed class IronPdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public IronPdfMigration()
        : this(new CanvasIronPdfMigration().AsPxaMigration())
    {
    }

    internal IronPdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
