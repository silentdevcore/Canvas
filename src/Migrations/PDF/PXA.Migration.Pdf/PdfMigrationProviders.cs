using PXA.Migration.Abstractions;
using ActivePdfMigration = PXA.Migration.Pdf.Code.ActivePdf.ActivePdfMigration;
using ApryseMigration = PXA.Migration.Pdf.Code.Apryse.ApryseMigration;
using AsposePdfMigration = PXA.Migration.Pdf.Code.Aspose.AsposePdfMigration;
using DevExpressPdfMigration = PXA.Migration.Pdf.Code.DevExpress.DevExpressPdfMigration;
using DsPdfMigration = PXA.Migration.Pdf.Code.DsPdf.DsPdfMigration;
using FoxitPdfMigration = PXA.Migration.Pdf.Code.Foxit.FoxitPdfMigration;
using GemBoxPdfMigration = PXA.Migration.Pdf.Code.GemBox.GemBoxPdfMigration;
using IronPdfMigration = PXA.Migration.Pdf.Code.IronPdf.IronPdfMigration;
using IText7Migration = PXA.Migration.Pdf.Code.IText7.IText7Migration;
using LeadtoolsPdfMigration = PXA.Migration.Pdf.Code.Leadtools.LeadtoolsPdfMigration;
using PdfKitNetMigration = PXA.Migration.Pdf.Code.PdfKitNet.PdfKitNetMigration;
using PdfToolsMigration = PXA.Migration.Pdf.Code.PdfTools.PdfToolsMigration;
using PdfToolsToolboxMigration = PXA.Migration.Pdf.Code.PdfToolsToolbox.PdfToolsToolboxMigration;
using SpirePdfMigration = PXA.Migration.Pdf.Code.Spire.SpirePdfMigration;
using SyncfusionPdfMigration = PXA.Migration.Pdf.Code.Syncfusion.SyncfusionPdfMigration;

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
