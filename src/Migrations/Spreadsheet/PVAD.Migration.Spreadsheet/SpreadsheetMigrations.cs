using PVAD.Migration.Abstractions;
using PvadAsposeCellsMigration = PVAD.Migration.Spreadsheet.Code.Aspose.AsposeCellsMigration;
using PvadClosedXmlSpreadsheetMigration = PVAD.Migration.Spreadsheet.Code.ClosedXml.ClosedXmlSpreadsheetMigration;
using PvadEpplusSpreadsheetMigration = PVAD.Migration.Spreadsheet.Code.Epplus.EpplusSpreadsheetMigration;
using PvadGemBoxSpreadsheetMigration = PVAD.Migration.Spreadsheet.Code.GemBox.GemBoxSpreadsheetMigration;
using PvadNpoiMigration = PVAD.Migration.Spreadsheet.Code.Npoi.NpoiMigration;
using PvadSpireXlsMigration = PVAD.Migration.Spreadsheet.Code.Spire.SpireXlsMigration;
using PvadSpreadsheetLightMigration = PVAD.Migration.Spreadsheet.Code.SpreadsheetLight.SpreadsheetLightMigration;
using PvadSyncfusionXlsIoMigration = PVAD.Migration.Spreadsheet.Code.Syncfusion.SyncfusionXlsIoMigration;

namespace PVAD.Migration.Spreadsheet;

public sealed class AsposeCellsMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PvadAsposeCellsMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class ClosedXmlSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PvadClosedXmlSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class EpplusSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PvadEpplusSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class GemBoxSpreadsheetMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PvadGemBoxSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class NpoiMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PvadNpoiMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SpireXlsMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PvadSpireXlsMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SpreadsheetLightMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PvadSpreadsheetLightMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}

public sealed class SyncfusionXlsIoMigration : ISourceMigration
{
    private readonly ISourceMigration inner = new PvadSyncfusionXlsIoMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);
}
