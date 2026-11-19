using PVAD.Migration.Abstractions;
using PVAD.Migration.Roslyn;
using PVAD.Licensing;
using PvadAsposeCellsMigration = PVAD.Migration.Spreadsheet.Code.Aspose.AsposeCellsMigration;
using PvadClosedXmlSpreadsheetMigration = PVAD.Migration.Spreadsheet.Code.ClosedXml.ClosedXmlSpreadsheetMigration;
using PvadEpplusSpreadsheetMigration = PVAD.Migration.Spreadsheet.Code.Epplus.EpplusSpreadsheetMigration;
using PvadGemBoxSpreadsheetMigration = PVAD.Migration.Spreadsheet.Code.GemBox.GemBoxSpreadsheetMigration;
using PvadNpoiMigration = PVAD.Migration.Spreadsheet.Code.Npoi.NpoiMigration;
using PvadSpireXlsMigration = PVAD.Migration.Spreadsheet.Code.Spire.SpireXlsMigration;
using PvadSpreadsheetLightMigration = PVAD.Migration.Spreadsheet.Code.SpreadsheetLight.SpreadsheetLightMigration;
using PvadSyncfusionXlsIoMigration = PVAD.Migration.Spreadsheet.Code.Syncfusion.SyncfusionXlsIoMigration;

namespace PVAD.Migration.Spreadsheet;

public sealed class AsposeCellsMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadAsposeCellsMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "aspose-cells", sourceCode);
}

public sealed class ClosedXmlSpreadsheetMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadClosedXmlSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "closedxml", sourceCode);
}

public sealed class EpplusSpreadsheetMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadEpplusSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "epplus", sourceCode);
}

public sealed class GemBoxSpreadsheetMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadGemBoxSpreadsheetMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "gembox-spreadsheet", sourceCode);
}

public sealed class NpoiMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadNpoiMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "npoi", sourceCode);
}

public sealed class SpireXlsMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadSpireXlsMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "spire-xls", sourceCode);
}

public sealed class SpreadsheetLightMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadSpreadsheetLightMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "spreadsheetlight", sourceCode);
}

public sealed class SyncfusionXlsIoMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadSyncfusionXlsIoMigration();

    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "syncfusion-xlsio", sourceCode);
}
