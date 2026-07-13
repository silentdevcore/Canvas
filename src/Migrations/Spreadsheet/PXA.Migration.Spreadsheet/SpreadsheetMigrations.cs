using PXA.Migration.Abstractions;
using PxaAsposeCellsMigration = PXA.Migration.Spreadsheet.Code.Aspose.AsposeCellsMigration;
using PxaClosedXmlSpreadsheetMigration = PXA.Migration.Spreadsheet.Code.ClosedXml.ClosedXmlSpreadsheetMigration;
using PxaEpplusSpreadsheetMigration = PXA.Migration.Spreadsheet.Code.Epplus.EpplusSpreadsheetMigration;
using PxaGemBoxSpreadsheetMigration = PXA.Migration.Spreadsheet.Code.GemBox.GemBoxSpreadsheetMigration;
using PxaNpoiMigration = PXA.Migration.Spreadsheet.Code.Npoi.NpoiMigration;
using PxaSpireXlsMigration = PXA.Migration.Spreadsheet.Code.Spire.SpireXlsMigration;
using PxaSpreadsheetLightMigration = PXA.Migration.Spreadsheet.Code.SpreadsheetLight.SpreadsheetLightMigration;
using PxaSyncfusionXlsIoMigration = PXA.Migration.Spreadsheet.Code.Syncfusion.SyncfusionXlsIoMigration;

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
