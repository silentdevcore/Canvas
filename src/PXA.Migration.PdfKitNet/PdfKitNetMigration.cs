using PXA.Migration.Abstractions;
using CanvasPdfKitNetMigration = Canvas.Migration.PdfKitNet.PdfKitNetMigration;

namespace PXA.Migration.PdfKitNet;

public sealed class PdfKitNetMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public PdfKitNetMigration()
        : this(new CanvasPdfKitNetMigration().AsPxaMigration())
    {
    }

    internal PdfKitNetMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
