using PXA.Migration.Abstractions;
using CanvasAsposePdfMigration = Canvas.Migration.AsposePdf.AsposePdfMigration;

namespace PXA.Migration.AsposePdf;

public sealed class AsposePdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public AsposePdfMigration()
        : this(new CanvasAsposePdfMigration().AsPxaMigration())
    {
    }

    internal AsposePdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
