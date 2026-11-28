using PVAD.Core.Contracts;
using PVAD.Licensing;

internal static class ProductGrantChecks
{
    internal static List<string> Profiles { get; } = [];

    internal static void Only(string product, DesignExportDto design, List<string> checks)
    {
        var id = PvadLicense.GetStatus().LicenseId;
        PvadLicense.Require(product);
        checks.Add($"single-product:{product}:admitted");
        foreach (var (other, provider) in new (string, string?)[]
        {
            ("pdf", null), ("word", null), ("spreadsheet", null), ("importer", null), ("converter", null),
            ("migration.pdf.code", "aspose-pdf"), ("migration.spreadsheet.code", "aspose-cells"),
            ("migration.pdf.viewer", "nutrient-viewer"), ("migration.pdf.designer", "nutrient-viewer"),
            ("migration.report.designer", "devexpress-report"),
        })
            if (other != product) Denied(() => PvadLicense.Require(other, provider), $"denied:{other}");
        if (product != "pdf") Denied(() => PVAD.Generator.Pdf.CreateDocument(), "public-pdf-denied");
        if (product != "word") Denied(() => PVAD.Generator.Word.Export(design), "public-word-denied");
        if (product != "spreadsheet") Denied(() => PVAD.Generator.Spreadsheet.CreateWorkbook(), "public-spreadsheet-denied");
        if (PvadLicense.GetStatus().LicenseId != id) throw new InvalidOperationException("A denial changed the active license.");
        checks.Add($"single-product:{product}:snapshot-unchanged");
        if (Profiles.Contains(product)) throw new InvalidOperationException("A single-product profile was exercised twice.");
        Profiles.Add(product);

        void Denied(Action call, string name)
        {
            try { call(); throw new InvalidOperationException("An unrelated product was admitted."); }
            catch (PvadLicenseException error) when (error.Code == "PVADLIC012")
            { checks.Add($"single-product:{product}:{name}"); }
        }
    }
}
