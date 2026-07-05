using PXA.Migration.Abstractions;
using CanvasAsposeCellsMigration = Canvas.Migration.AsposeCells.AsposeCellsMigration;
using CanvasClosedXmlSpreadsheetMigration = Canvas.Migration.ClosedXmlSpreadsheet.ClosedXmlSpreadsheetMigration;
using CanvasEpplusSpreadsheetMigration = Canvas.Migration.EpplusSpreadsheet.EpplusSpreadsheetMigration;
using CanvasGemBoxSpreadsheetMigration = Canvas.Migration.GemBoxSpreadsheet.GemBoxSpreadsheetMigration;
using CanvasNpoiMigration = Canvas.Migration.Npoi.NpoiMigration;
using CanvasSpireXlsMigration = Canvas.Migration.SpireXls.SpireXlsMigration;
using CanvasSpreadsheetLightMigration = Canvas.Migration.SpreadsheetLight.SpreadsheetLightMigration;
using CanvasSyncfusionXlsIoMigration = Canvas.Migration.SyncfusionXlsIo.SyncfusionXlsIoMigration;

namespace PXA.Migration.Spreadsheet;

public sealed class AsposeCellsMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasAsposeCellsMigration().AsPxaMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class ClosedXmlSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasClosedXmlSpreadsheetMigration().AsPxaMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class EpplusSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasEpplusSpreadsheetMigration().AsPxaMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class GemBoxSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasGemBoxSpreadsheetMigration().AsPxaMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class NpoiMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasNpoiMigration().AsPxaMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SpireXlsMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasSpireXlsMigration().AsPxaMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SpreadsheetLightMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasSpreadsheetLightMigration().AsPxaMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SyncfusionXlsIoMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasSyncfusionXlsIoMigration().AsPxaMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}
