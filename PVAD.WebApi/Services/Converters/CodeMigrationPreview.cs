using PVAD.Licensing;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Pdf;
using PVAD.Migration.Roslyn;
using PVAD.Migration.Spreadsheet;

namespace PVAD.WebApi.Services.Converters;

internal static class CodeMigrationPreview
{
    internal static IDisposable? Begin(BasePdfConverter converter) =>
        TryResolve(converter, out var provider)
            ? CodeMigrationAdmission.Begin(Product(provider), provider.ProviderKey)
            : null;

    internal static string Convert(BasePdfConverter converter, string source)
    {
        if (!TryResolve(converter, out var provider)) return converter.ConvertCode(source);
        return provider.Domain == MigrationDomains.Pdf
            ? PdfMigrationProviders.MigrateAdmitted(provider.ProviderKey, source).MigratedCode
            : SpreadsheetMigrationProviders.MigrateAdmitted(provider.ProviderKey, source).MigratedCode;
    }

    private static string Product(MigrationProviderDescriptor provider) => provider.Domain == MigrationDomains.Pdf
        ? PvadLicenseProducts.PdfCodeMigration : PvadLicenseProducts.SpreadsheetCodeMigration;

    private static bool TryResolve(BasePdfConverter converter, out MigrationProviderDescriptor provider)
    {
        provider = null!;
        var type = converter.GetType();
        if (type.Assembly != typeof(CodeMigrationPreview).Assembly || !type.IsSealed) return false;
        var domain = converter.Kind == MigrationDomains.Spreadsheet ? MigrationDomains.Spreadsheet : MigrationDomains.Pdf;
        return MigrationProviderCatalog.TryGet(domain, MigrationKinds.Code, converter.FrameworkId, out provider);
    }
}
