using System.Reflection;
using System.Text.Json;
using PVAD.Core.Abstractions;
using PVAD.Core.Contracts;
using PVAD.Licensing;

internal static class FreeContractChecks
{
    internal static string[] Run(DesignExportDto design, List<string> checks)
    {
        Unlicensed();
        var types = typeof(DesignExportDto).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == "PVAD.Core.Contracts" && type.IsClass && !type.IsAbstract
                && !type.ContainsGenericParameters && type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        if (types.Length == 0) throw new InvalidOperationException("Missing Core contract coverage.");
        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type)!;
            foreach (var property in type.GetProperties().Where(property => property.CanWrite && property.PropertyType == typeof(JsonElement)))
                property.SetValue(instance, JsonSerializer.SerializeToElement("synthetic"));
            var json = JsonSerializer.Serialize(instance, type);
            var restored = JsonSerializer.Deserialize(json, type)!;
            Check(JsonSerializer.Serialize(restored, type) == json, "core-dto:" + type.FullName);
        }
        Roundtrip(design, "populated-design");
        Roundtrip(new SpreadsheetDto { Id = "contract-workbook", Sheets = [new SheetDto { Id = "contract-sheet",
            Cells = [new CellDto { Row = 0, Col = 0, Value = JsonSerializer.SerializeToElement(42) }] }] }, "populated-spreadsheet");
        Roundtrip(new PVAD.Domain.Entities.DesignTemplate { Id = "contract-template", Name = "Synthetic", Elements = [],
            PageSettings = new PVAD.Domain.ValueObjects.PageSettings() }, "domain-template");
        Roundtrip(new PVAD.Pdf.PdfSaveOptions { CompressContentStreams = true }, "pdf-save-options");
        Roundtrip(new PVAD.Pdf.PdfDrawTextOptions { FontSize = 14, Bold = true }, "pdf-text-options");
        var info = new PVAD.Pdf.PdfDocumentInfo { Title = "Synthetic metadata" };
        info.CustomProperties["synthetic"] = "metadata-only";
        Check(info.Title == "Synthetic metadata" && info.CustomProperties["synthetic"] == "metadata-only", "pdf-metadata-edit");
        Check(new ExportOptions(Dpi: 72).Dpi == 72, "export-options");
        IRendererCapabilities[] capabilities = [new PVAD.Infrastructure.Pdf.PdfRendererCapabilities(),
            new PVAD.Infrastructure.Word.WordRendererCapabilities(), new PVAD.Infrastructure.Spreadsheet.SheetRendererCapabilities()];
        foreach (var capability in capabilities)
        {
            Check(!string.IsNullOrWhiteSpace(capability.RendererKey), "renderer-key:" + capability.RendererKey);
            foreach (var property in typeof(IRendererCapabilities).GetProperties())
                _ = property.GetValue(capability);
            Check(true, "renderer-capabilities:" + capability.RendererKey);
        }
#if PVAD_IMPORTER_PACKAGE_PROBE
        Roundtrip(new PVAD.Importer.Parsing.PdfParseLimits(), "importer-limits");
        Roundtrip(new PVAD.Importer.Saving.PdfFullRewriteOptions(), "importer-save-options");
        Roundtrip(new PVAD.FileImporter.ImageOcr.OcrWorkerRequest { Languages = "eng",
            Pages = [new() { EncodedImagePath = "synthetic.png", WidthPx = 160, HeightPx = 80 }] }, "ocr-request-data");
        Roundtrip(new PVAD.FileImporter.ImageOcr.OcrWorkerResponse { Success = false }, "ocr-response-data");
        var model = new PVAD.Importer.Document.PdfDocumentModel();
        model.AddPage(new(null, new PVAD.Importer.Objects.PdfDictionary()));
        Check(model.Pages.Count == 1, "detached-importer-model");
        foreach (var key in PVAD.FileImporter.FileImporterRegistry.Keys)
            Check(PVAD.FileImporter.FileImporterRegistry.Create(key) is PVAD.FileImporter.IFileImporter, "importer-factory:" + key);
#endif
#if PVAD_EXECUTION_PACKAGE_PROBE
        var exporters = typeof(PVAD.Infrastructure.Converters.DocumentExporter).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(IDocumentExporter).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
        foreach (var type in exporters)
            Check(!string.IsNullOrWhiteSpace(((IDocumentExporter)Activator.CreateInstance(type)!).FormatKey), "exporter-format:" + type.Name);
#endif
#if PVAD_MIGRATION_PACKAGE_PROBE
        foreach (var descriptor in PVAD.Migration.Abstractions.MigrationProviderCatalog.All)
        {
            Roundtrip(descriptor, "provider-descriptor:" + descriptor.Domain + ":" + descriptor.MigrationKind + ":" + descriptor.ProviderKey);
            Check(!string.IsNullOrWhiteSpace(descriptor.PermissionKey), "provider-permission:" + descriptor.Domain + ":" + descriptor.MigrationKind + ":" + descriptor.ProviderKey);
        }
        foreach (var key in PVAD.Migration.Pdf.PdfMigrationProviders.Keys)
            Check(PVAD.Migration.Pdf.PdfMigrationProviders.Create(key) is not null, "pdf-provider-factory:" + key);
        foreach (var key in PVAD.Migration.Spreadsheet.SpreadsheetMigrationProviders.Keys)
            Check(PVAD.Migration.Spreadsheet.SpreadsheetMigrationProviders.Create(key) is not null, "sheet-provider-factory:" + key);
        foreach (var key in PVAD.Migration.Report.ReportMigrationProviders.Keys)
            Check(PVAD.Migration.Report.ReportMigrationProviders.Create(key) is not null, "report-provider-factory:" + key);
        Denied(() => PVAD.Migration.Pdf.PdfMigrationProviders.Create("aspose-pdf").Migrate("var value = 42;"), "pdf-migration-still-protected");
        Denied(() => PVAD.Migration.Spreadsheet.SpreadsheetMigrationProviders.Create("aspose-cells").Migrate("var value = 42;"), "sheet-migration-still-protected");
        Denied(() => PVAD.Migration.Report.ReportMigrationProviders.Create("devexpress-report").Convert("synthetic"), "report-migration-still-protected");
#endif
        Unlicensed();
        Check(true, "state-remains-unlicensed");
        return types.Select(type => type.FullName!).ToArray();

        void Unlicensed()
        {
            if (PvadLicense.GetStatus().State != PvadLicenseState.Unlicensed || PvadLicense.GetStatus().CanStartOperation)
                throw new InvalidOperationException("Data contracts or metadata activated a license.");
        }
        void Check(bool valid, string name)
        {
            if (!valid) throw new InvalidOperationException("Unlicensed contract failed: " + name);
            checks.Add("unlicensed-contract:" + name);
        }
        void Roundtrip<T>(T value, string name)
        {
            var json = JsonSerializer.Serialize(value);
            Check(JsonSerializer.Serialize(JsonSerializer.Deserialize<T>(json)) == json, name);
        }
        void Denied(Action operation, string name)
        {
            try { operation(); throw new InvalidOperationException("Metadata granted processing rights."); }
            catch (PvadLicenseException error) when (error.Code == "PVADLIC001") { Check(true, name); }
        }
    }
}
