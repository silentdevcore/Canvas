using PXA.Migration.Abstractions;
using PxaAsposeCellsMigration = PXA.Migration.AsposeCells.AsposeCellsMigration;
using PxaClosedXmlSpreadsheetMigration = PXA.Migration.ClosedXmlSpreadsheet.ClosedXmlSpreadsheetMigration;
using PxaEpplusSpreadsheetMigration = PXA.Migration.EpplusSpreadsheet.EpplusSpreadsheetMigration;
using PxaGemBoxSpreadsheetMigration = PXA.Migration.GemBoxSpreadsheet.GemBoxSpreadsheetMigration;
using PxaNpoiMigration = PXA.Migration.Npoi.NpoiMigration;
using PxaSpireXlsMigration = PXA.Migration.SpireXls.SpireXlsMigration;
using PxaSpreadsheetLightMigration = PXA.Migration.SpreadsheetLight.SpreadsheetLightMigration;
using PxaSyncfusionXlsIoMigration = PXA.Migration.SyncfusionXlsIo.SyncfusionXlsIoMigration;

namespace PXA.Migration.Spreadsheet;

public sealed class AsposeCellsMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PxaAsposeCellsMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class ClosedXmlSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PxaClosedXmlSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class EpplusSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PxaEpplusSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class GemBoxSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PxaGemBoxSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class NpoiMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PxaNpoiMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SpireXlsMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PxaSpireXlsMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SpreadsheetLightMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PxaSpreadsheetLightMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SyncfusionXlsIoMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PxaSyncfusionXlsIoMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}
