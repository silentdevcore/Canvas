using PVAD.Licensing;
using PVAD.Migration.Abstractions;

namespace PVAD.Migration.Roslyn;

internal interface IAdmittedCodeMigration
{
    MigrationResult MigrateAdmitted(string sourceCode);
}

internal static class CodeMigrationAdmission
{
    internal static IDisposable Begin(string product, string provider) =>
        PvadLicense.BeginOperation(OperationFor(product), CanonicalProvider(product, provider));

    internal static void Demand(string product, string provider) =>
        PvadLicense.DemandMigrationContinuation(product, CanonicalProvider(product, provider));

    internal static MigrationResult MigrateAdmitted(ISourceMigration migration, string product, string provider, string sourceCode)
    {
        Demand(product, provider);
        if (migration is not IAdmittedCodeMigration native)
            throw new InvalidOperationException("Only a built-in migration can continue an admitted operation.");
        return native.MigrateAdmitted(sourceCode);
    }

    private static PvadNativeOperation OperationFor(string product) => product switch
    {
        PvadLicenseProducts.PdfCodeMigration => PvadNativeOperation.PdfCodeMigration,
        PvadLicenseProducts.SpreadsheetCodeMigration => PvadNativeOperation.SpreadsheetCodeMigration,
        _ => throw new ArgumentException("Expected a native Code migration product.", nameof(product)),
    };

    private static string CanonicalProvider(string product, string provider)
    {
        _ = OperationFor(product);
        var domain = product == PvadLicenseProducts.PdfCodeMigration ? MigrationDomains.Pdf : MigrationDomains.Spreadsheet;
        return MigrationProviderCatalog.Get(domain, MigrationKinds.Code, provider).ProviderKey;
    }
}
