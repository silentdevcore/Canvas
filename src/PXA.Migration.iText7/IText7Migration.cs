using PXA.Migration.Abstractions;
using CanvasIText7Migration = Canvas.Migration.iText7.IText7Migration;

namespace PXA.Migration.iText7;

public sealed class IText7Migration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public IText7Migration()
        : this(new CanvasIText7Migration().AsPxaMigration())
    {
    }

    internal IText7Migration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
