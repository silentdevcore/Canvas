using CanvasSourceMigration = Canvas.Migration.Abstractions.ISourceMigration;

namespace PXA.Migration.Abstractions;

public static class SourceMigrationAdapters
{
    public static ISourceMigration AsPxaMigration(this CanvasSourceMigration migration)
    {
        return new CanvasSourceMigrationAdapter(migration);
    }
}
