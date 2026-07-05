using PXA.Migration.Abstractions;
using CanvasPdfToolsMigration = Canvas.Migration.PdfTools.PdfToolsMigration;

namespace PXA.Migration.PdfTools;

public sealed class PdfToolsMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public PdfToolsMigration()
        : this(new CanvasPdfToolsMigration().AsPxaMigration())
    {
    }

    internal PdfToolsMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
