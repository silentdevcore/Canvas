using PXA.Migration.Abstractions;
using CanvasGemBoxPdfMigration = Canvas.Migration.GemBoxPdf.GemBoxPdfMigration;

namespace PXA.Migration.GemBoxPdf;

public sealed class GemBoxPdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public GemBoxPdfMigration()
        : this(new CanvasGemBoxPdfMigration().AsPxaMigration())
    {
    }

    internal GemBoxPdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
