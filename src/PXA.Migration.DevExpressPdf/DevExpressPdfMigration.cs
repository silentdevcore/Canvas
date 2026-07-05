using PXA.Migration.Abstractions;
using CanvasDevExpressPdfMigration = Canvas.Migration.DevExpressPdf.DevExpressPdfMigration;

namespace PXA.Migration.DevExpressPdf;

public sealed class DevExpressPdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public DevExpressPdfMigration()
        : this(new CanvasDevExpressPdfMigration().AsPxaMigration())
    {
    }

    internal DevExpressPdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
