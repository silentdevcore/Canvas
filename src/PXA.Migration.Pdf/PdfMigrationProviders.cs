using PXA.Migration.Abstractions;
using ActivePdfMigration = PXA.Migration.ActivePdf.ActivePdfMigration;
using ApryseMigration = PXA.Migration.Apryse.ApryseMigration;
using AsposePdfMigration = PXA.Migration.AsposePdf.AsposePdfMigration;
using DevExpressPdfMigration = PXA.Migration.DevExpressPdf.DevExpressPdfMigration;
using DsPdfMigration = PXA.Migration.DsPdf.DsPdfMigration;
using FoxitPdfMigration = PXA.Migration.FoxitPdf.FoxitPdfMigration;
using GemBoxPdfMigration = PXA.Migration.GemBoxPdf.GemBoxPdfMigration;
using IronPdfMigration = PXA.Migration.IronPdf.IronPdfMigration;
using IText7Migration = PXA.Migration.iText7.IText7Migration;
using LeadtoolsPdfMigration = PXA.Migration.LeadtoolsPdf.LeadtoolsPdfMigration;
using PdfKitNetMigration = PXA.Migration.PdfKitNet.PdfKitNetMigration;
using PdfToolsMigration = PXA.Migration.PdfTools.PdfToolsMigration;
using PdfToolsToolboxMigration = PXA.Migration.PdfToolsToolbox.PdfToolsToolboxMigration;
using SpirePdfMigration = PXA.Migration.SpirePdf.SpirePdfMigration;
using SyncfusionPdfMigration = PXA.Migration.SyncfusionPdf.SyncfusionPdfMigration;

namespace PXA.Migration.Pdf;

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

        throw new KeyNotFoundException($"Unknown PXA PDF migration provider key '{key}'.");
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
