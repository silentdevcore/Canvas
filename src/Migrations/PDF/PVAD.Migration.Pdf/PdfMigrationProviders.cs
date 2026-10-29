using PVAD.Migration.Abstractions;
using PVAD.Migration.Roslyn;
using ActivePdfMigration = PVAD.Migration.Pdf.Code.ActivePdf.ActivePdfMigration;
using ApryseMigration = PVAD.Migration.Pdf.Code.Apryse.ApryseMigration;
using AsposePdfMigration = PVAD.Migration.Pdf.Code.Aspose.AsposePdfMigration;
using DevExpressPdfMigration = PVAD.Migration.Pdf.Code.DevExpress.DevExpressPdfMigration;
using DsPdfMigration = PVAD.Migration.Pdf.Code.DsPdf.DsPdfMigration;
using FoxitPdfMigration = PVAD.Migration.Pdf.Code.Foxit.FoxitPdfMigration;
using GemBoxPdfMigration = PVAD.Migration.Pdf.Code.GemBox.GemBoxPdfMigration;
using IronPdfMigration = PVAD.Migration.Pdf.Code.IronPdf.IronPdfMigration;
using IText7Migration = PVAD.Migration.Pdf.Code.IText7.IText7Migration;
using LeadtoolsPdfMigration = PVAD.Migration.Pdf.Code.Leadtools.LeadtoolsPdfMigration;
using NutrientMigration = PVAD.Migration.Pdf.Code.Nutrient.NutrientMigration;
using PdfKitNetMigration = PVAD.Migration.Pdf.Code.PdfKitNet.PdfKitNetMigration;
using PdfToolsMigration = PVAD.Migration.Pdf.Code.PdfTools.PdfToolsMigration;
using PdfToolsToolboxMigration = PVAD.Migration.Pdf.Code.PdfToolsToolbox.PdfToolsToolboxMigration;
using SpirePdfMigration = PVAD.Migration.Pdf.Code.Spire.SpirePdfMigration;
using SyncfusionPdfMigration = PVAD.Migration.Pdf.Code.Syncfusion.SyncfusionPdfMigration;

namespace PVAD.Migration.Pdf;

public static class PdfMigrationProviders
{
    private static readonly IReadOnlyDictionary<string, Func<ISourceMigration>> Factories =
        new Dictionary<string, Func<ISourceMigration>>(StringComparer.OrdinalIgnoreCase)
        {
            [PdfMigrationProviderKeys.ActivePdf] = static () => new ActivePdfMigration(),
            [PdfMigrationProviderKeys.Apryse] = static () => new ApryseMigration(),
            [PdfMigrationProviderKeys.AsposePdf] = static () => new AsposePdfMigration(),
            [PdfMigrationProviderKeys.DevExpressPdf] = static () => new DevExpressPdfMigration(),
            [PdfMigrationProviderKeys.DsPdf] = static () => new DsPdfMigration(),
            [PdfMigrationProviderKeys.FoxitPdf] = static () => new FoxitPdfMigration(),
            [PdfMigrationProviderKeys.GemBoxPdf] = static () => new GemBoxPdfMigration(),
            [PdfMigrationProviderKeys.IronPdf] = static () => new IronPdfMigration(),
            [PdfMigrationProviderKeys.IText7] = static () => new IText7Migration(),
            [PdfMigrationProviderKeys.LeadtoolsPdf] = static () => new LeadtoolsPdfMigration(),
            [PdfMigrationProviderKeys.Nutrient] = static () => new NutrientMigration(),
            [PdfMigrationProviderKeys.PdfKitNet] = static () => new PdfKitNetMigration(),
            [PdfMigrationProviderKeys.PdfTools] = static () => new PdfToolsMigration(),
            [PdfMigrationProviderKeys.PdfToolsToolbox] = static () => new PdfToolsToolboxMigration(),
            [PdfMigrationProviderKeys.SpirePdf] = static () => new SpirePdfMigration(),
            [PdfMigrationProviderKeys.SyncfusionPdf] = static () => new SyncfusionPdfMigration(),
        };

    public static IReadOnlyList<string> Keys { get; } = Factories.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();

    public static ISourceMigration Create(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (TryCreate(key, out var migration))
            return migration;

        throw new KeyNotFoundException($"Unknown PVAD PDF migration provider key '{key}'.");
    }

    public static MigrationResult Migrate(string key, string sourceCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCode);
        var migration = Create(key);
        var result = migration.Migrate(sourceCode);
        var trust = result.PdfTrust ?? CSharpPdfTrustEvidenceScanner.Scan(
            sourceCode,
            PdfTrustMigrationCatalog.Get(key));
        var diagnostics = result.Diagnostics.ToList();
        foreach (var evidence in trust.Evidence)
        {
            if (diagnostics.Any(item => string.Equals(item.Id, evidence.DiagnosticId, StringComparison.Ordinal)))
                continue;
            diagnostics.Add(new MigrationDiagnostic
            {
                Id = evidence.DiagnosticId,
                Severity = MigrationDiagnosticSeverity.Warning,
                Message = $"{evidence.SourceOperation} requires {evidence.Disposition} PDF Trust migration. See {evidence.DocumentationPath}."
            });
        }

        return new MigrationResult
        {
            MigratedCode = result.MigratedCode,
            Diagnostics = diagnostics,
            PdfTrust = trust
        };
    }

    public static bool TryCreate(string key, out ISourceMigration migration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Factories.TryGetValue(key, out var factory))
        {
            migration = factory();
            return true;
        }

        migration = null!;
        return false;
    }
}
