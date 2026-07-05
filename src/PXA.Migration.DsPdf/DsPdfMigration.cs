using PXA.Migration.Abstractions;
using CanvasDsPdfMigration = Canvas.Migration.DsPdf.DsPdfMigration;

namespace PXA.Migration.DsPdf;

public sealed class DsPdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public DsPdfMigration()
        : this(new CanvasDsPdfMigration().AsPxaMigration())
    {
    }

    internal DsPdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
