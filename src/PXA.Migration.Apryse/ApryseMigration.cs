using PXA.Migration.Abstractions;
using CanvasApryseMigration = Canvas.Migration.Apryse.ApryseMigration;

namespace PXA.Migration.Apryse;

public sealed class ApryseMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public ApryseMigration()
        : this(new CanvasApryseMigration().AsPxaMigration())
    {
    }

    internal ApryseMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
