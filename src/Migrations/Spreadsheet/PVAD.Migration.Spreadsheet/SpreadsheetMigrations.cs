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

/// <summary>Adapts Aspose.Cells source migration through the aggregate Spreadsheet package.</summary>
/// <remarks>Conversion requires native Spreadsheet Code Migration rights for <c>aspose-cells</c>.</remarks>
public sealed class AsposeCellsMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadAsposeCellsMigration();

    /// <inheritdoc cref="ISourceMigration.Migrate"/>
    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "aspose-cells", sourceCode);
}

/// <summary>Adapts ClosedXML source migration through the aggregate Spreadsheet package.</summary>
/// <remarks>Conversion requires native Spreadsheet Code Migration rights for <c>closedxml</c>.</remarks>
public sealed class ClosedXmlSpreadsheetMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadClosedXmlSpreadsheetMigration();

    /// <inheritdoc cref="ISourceMigration.Migrate"/>
    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "closedxml", sourceCode);
}

/// <summary>Adapts EPPlus source migration through the aggregate Spreadsheet package.</summary>
/// <remarks>Conversion requires native Spreadsheet Code Migration rights for <c>epplus</c>.</remarks>
public sealed class EpplusSpreadsheetMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadEpplusSpreadsheetMigration();

    /// <inheritdoc cref="ISourceMigration.Migrate"/>
    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "epplus", sourceCode);
}

/// <summary>Adapts GemBox.Spreadsheet source migration through the aggregate Spreadsheet package.</summary>
/// <remarks>Conversion requires native Spreadsheet Code Migration rights for <c>gembox-spreadsheet</c>.</remarks>
public sealed class GemBoxSpreadsheetMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadGemBoxSpreadsheetMigration();

    /// <inheritdoc cref="ISourceMigration.Migrate"/>
    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "gembox-spreadsheet", sourceCode);
}

/// <summary>Adapts NPOI source migration through the aggregate Spreadsheet package.</summary>
/// <remarks>Conversion requires native Spreadsheet Code Migration rights for <c>npoi</c>.
/// Native rights do not replace third-party usage or distribution obligations.</remarks>
public sealed class NpoiMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadNpoiMigration();

    /// <inheritdoc cref="ISourceMigration.Migrate"/>
    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "npoi", sourceCode);
}

/// <summary>Adapts Spire.XLS source migration through the aggregate Spreadsheet package.</summary>
/// <remarks>Conversion requires native Spreadsheet Code Migration rights for <c>spire-xls</c>.</remarks>
public sealed class SpireXlsMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadSpireXlsMigration();

    /// <inheritdoc cref="ISourceMigration.Migrate"/>
    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "spire-xls", sourceCode);
}

/// <summary>Adapts SpreadsheetLight source migration through the aggregate Spreadsheet package.</summary>
/// <remarks>Conversion requires native Spreadsheet Code Migration rights for <c>spreadsheetlight</c>.</remarks>
public sealed class SpreadsheetLightMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadSpreadsheetLightMigration();

    /// <inheritdoc cref="ISourceMigration.Migrate"/>
    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "spreadsheetlight", sourceCode);
}

/// <summary>Adapts Syncfusion XlsIO source migration through the aggregate Spreadsheet package.</summary>
/// <remarks>Conversion requires native Spreadsheet Code Migration rights for <c>syncfusion-xlsio</c>.</remarks>
public sealed class SyncfusionXlsIoMigration : ISourceMigration, IAdmittedCodeMigration
{
    private readonly ISourceMigration inner = new PvadSyncfusionXlsIoMigration();

    /// <inheritdoc cref="ISourceMigration.Migrate"/>
    public MigrationResult Migrate(string sourceCode) => inner.Migrate(sourceCode);

    MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode) =>
        CodeMigrationAdmission.MigrateAdmitted(inner, PvadLicenseProducts.SpreadsheetCodeMigration, "syncfusion-xlsio", sourceCode);
}
