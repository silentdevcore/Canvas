using PXA.Migration.Abstractions;
using CanvasLeadtoolsPdfMigration = Canvas.Migration.LeadtoolsPdf.LeadtoolsPdfMigration;

namespace PXA.Migration.LeadtoolsPdf;

public sealed class LeadtoolsPdfMigration : ISourceMigration
{
    private readonly ISourceMigration inner;

    public LeadtoolsPdfMigration()
        : this(new CanvasLeadtoolsPdfMigration().AsPxaMigration())
    {
    }

    internal LeadtoolsPdfMigration(ISourceMigration inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public MigrationResult Migrate(string sourceCode)
    {
        return inner.Migrate(sourceCode);
    }
}
