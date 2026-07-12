using PXA.Migration.Abstractions;
using CanvasAsposeCellsMigration = PXA.Migration.AsposeCells.AsposeCellsMigration;
using CanvasClosedXmlSpreadsheetMigration = PXA.Migration.ClosedXmlSpreadsheet.ClosedXmlSpreadsheetMigration;
using CanvasEpplusSpreadsheetMigration = PXA.Migration.EpplusSpreadsheet.EpplusSpreadsheetMigration;
using CanvasGemBoxSpreadsheetMigration = PXA.Migration.GemBoxSpreadsheet.GemBoxSpreadsheetMigration;
using CanvasNpoiMigration = PXA.Migration.Npoi.NpoiMigration;
using CanvasSpireXlsMigration = PXA.Migration.SpireXls.SpireXlsMigration;
using CanvasSpreadsheetLightMigration = PXA.Migration.SpreadsheetLight.SpreadsheetLightMigration;
using CanvasSyncfusionXlsIoMigration = PXA.Migration.SyncfusionXlsIo.SyncfusionXlsIoMigration;

namespace PXA.Migration.Spreadsheet;

public sealed class AsposeCellsMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasAsposeCellsMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class ClosedXmlSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasClosedXmlSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class EpplusSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasEpplusSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class GemBoxSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasGemBoxSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class NpoiMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasNpoiMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SpireXlsMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasSpireXlsMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SpreadsheetLightMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasSpreadsheetLightMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SyncfusionXlsIoMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new CanvasSyncfusionXlsIoMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}
