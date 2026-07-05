using PXA.Migration.Abstractions;
using CanvasSpirePdfMigration = Canvas.Migration.SpirePdf.SpirePdfMigration;

namespace PXA.Migration.SpirePdf;

public sealed class SpirePdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public SpirePdfMigration()
        : this(new CanvasSpirePdfMigration().AsPxaMigration())
    {
    }

    internal SpirePdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
