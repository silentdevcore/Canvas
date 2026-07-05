using PXA.Migration.Abstractions;
using CanvasPdfToolsToolboxMigration = Canvas.Migration.PdfToolsToolbox.PdfToolsToolboxMigration;

namespace PXA.Migration.PdfToolsToolbox;

public sealed class PdfToolsToolboxMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public PdfToolsToolboxMigration()
        : this(new CanvasPdfToolsToolboxMigration().AsPxaMigration())
    {
    }

    internal PdfToolsToolboxMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
