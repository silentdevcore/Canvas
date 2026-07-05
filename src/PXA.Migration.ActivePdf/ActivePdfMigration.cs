using PXA.Migration.Abstractions;
using CanvasActivePdfMigration = Canvas.Migration.ActivePdf.ActivePdfMigration;

namespace PXA.Migration.ActivePdf;

public sealed class ActivePdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public ActivePdfMigration()
        : this(new CanvasActivePdfMigration().AsPxaMigration())
    {
    }

    internal ActivePdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
