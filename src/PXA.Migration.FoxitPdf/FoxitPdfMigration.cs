using PXA.Migration.Abstractions;
using CanvasFoxitPdfMigration = Canvas.Migration.FoxitPdf.FoxitPdfMigration;

namespace PXA.Migration.FoxitPdf;

public sealed class FoxitPdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public FoxitPdfMigration()
        : this(new CanvasFoxitPdfMigration().AsPxaMigration())
    {
    }

    internal FoxitPdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
