using System.Text;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Spreadsheet;
using PVAD.Licensing;

internal static class SpreadsheetContinuationChecks
{
    internal static int Run(string[] licenses, List<string> checks, string directory)
    {
        var start = checks.Count;
        var exporter = new ExcelWorkbookExporter();
        var importer = new ExcelWorkbookImporter();
        var xls = new XlsWorkbookIo();
        var calculator = new SpreadsheetCalculator();
        foreach (var kind in new[] { "xlsx", "xls", "csv", "tsv", "calculate", "builder-xlsx", "save-xlsx", "save-xls", "save-csv", "save-tsv" })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var workbook = new PvadWorkbook("Synthetic continuation");
            workbook.AddSheet("Data");
            var dto = workbook.ToWorkbook();
            var changes = 0;
            var value = new CallbackValue(() =>
            {
                if (changes++ != 0) return;
                PvadLicense.LoadFromFile(licenses[1]);
                Denied(() => new PvadWorkbook(), kind + ":reentrant-authoring-denied");
                Denied(() => exporter.Export(new()), kind + ":reentrant-export-denied");
                Denied(() => calculator.Calculate(new()), kind + ":reentrant-calculation-denied");
            });
            dto.Sheets[0].Cells.AddRange([
                new() { Row = 0, Col = 0, Type = "text", Value = value },
                new() { Row = 1, Col = 0, Type = "number", Value = 3 },
                new() { Row = 2, Col = 0, Type = "number", Value = 1 },
                new() { Row = 3, Col = 0, Type = "formula", Formula = "=SUM(A2:A3)", Value = -77 },
            ]);
            byte[]? bytes = null;
            string? text = null;
            var extension = kind.StartsWith("save-", StringComparison.Ordinal) ? kind[5..] : kind;
            var path = Path.Combine(directory, "continued-" + kind + "." + extension);
            switch (kind)
            {
                case "xlsx": bytes = exporter.Export(dto, recalculate: true); break;
                case "xls": bytes = xls.Export(dto); break;
                case "csv": text = CsvSheetIo.ToCsv(dto.Sheets[0]); break;
                case "tsv": text = CsvSheetIo.ToCsv(dto.Sheets[0], '\t'); break;
                case "calculate": Check(ReferenceEquals(calculator.Calculate(dto), dto) && Convert.ToDouble(dto.Sheets[0].Cells[3].Value) == 4, kind + ":calculated-result"); break;
                case "builder-xlsx": bytes = workbook.ToXlsx(recalculate: true); break;
                default: workbook.Save(path); bytes = File.ReadAllBytes(path); break;
            }
            Check(changes > 0 && ReferenceEquals(dto.Sheets[0].Cells[0].Value, value) && (double)Convert.ToDouble(dto.Sheets[0].Cells[1].Value) == 3, kind + ":callback-and-retained-input");
            Denied(() => workbook.Save(path), kind + ":next-save-denied");
            Denied(() => exporter.Export(dto), kind + ":next-export-denied");
            Denied(() => CsvSheetIo.ToCsv(dto.Sheets[0]), kind + ":next-csv-denied");
            Denied(() => calculator.Calculate(dto), kind + ":next-calculation-denied");
            PvadLicense.LoadFromFile(licenses[0]);
            foreach (var product in new[] { PvadLicenseProducts.Pdf, PvadLicenseProducts.Word, PvadLicenseProducts.Importer, PvadLicenseProducts.Converter })
                Denied(() => PvadLicense.Require(product), kind + ":no-additional-product:" + product);
            if (kind is "xlsx" or "builder-xlsx" or "save-xlsx")
            {
                using var input = new MemoryStream(bytes!);
                var result = importer.Import(input);
                Check(bytes!.AsSpan().StartsWith("PK"u8) && result.Sheets[0].Cells.Any(c => c.Value?.ToString() == "Synthetic continuation value") && result.Sheets[0].Cells.Any(c => c.Formula == "=SUM(A2:A3)"), kind + ":xlsx-content-and-formula");
                if (kind != "save-xlsx") Check(Convert.ToDouble(result.Sheets[0].Cells.Single(c => c.Type == "formula").Value) == 4, kind + ":cached-calculated-value");
            }
            else if (kind is "xls" or "save-xls")
            {
                using var input = new MemoryStream(bytes!);
                var result = xls.Import(input);
                Check(bytes!.AsSpan().StartsWith(new byte[] { 0xd0, 0xcf, 0x11, 0xe0 }) && result.Sheets[0].Cells.Any(c => c.Value?.ToString() == "Synthetic continuation value") && result.Sheets[0].Cells.Any(c => c.Formula == "=SUM(A2:A3)"), kind + ":xls-content-and-formula");
            }
            else if (kind != "calculate")
            {
                text ??= Encoding.UTF8.GetString(bytes!);
                Check(text.StartsWith("Synthetic continuation value\r\n3\r\n1", StringComparison.Ordinal), kind + ":delimited-values");
            }
        }

        foreach (var error in new[] { "native-license", "source-io", "file-io" })
        foreach (var extension in new[] { "xlsx", "xls", "csv", "tsv" })
        {
            var name = error + ":" + extension;
            PvadLicense.LoadFromFile(licenses[0]);
            var workbook = new PvadWorkbook(); workbook.AddSheet("Data");
            workbook.ToWorkbook().Sheets[0].Cells.Add(new()
            {
                Row = 0, Col = 0, Type = "text", Value = new CallbackValue(() =>
                {
                    PvadLicense.LoadFromFile(licenses[1]);
                    if (error == "native-license") PvadLicense.Require(PvadLicenseProducts.Spreadsheet);
                    if (error == "source-io") throw new IOException("Synthetic sheet value failure.");
                }),
            });
            var path = Path.Combine(directory, error == "file-io" ? "missing-directory" : "", "failure." + extension);
            if (error != "file-io") File.WriteAllText(path, "sentinel");
            try { workbook.Save(path); throw new Exception("Expected spreadsheet failure: " + name); }
            catch (Exception e) when (error switch
            {
                "native-license" => e is PvadLicenseException { Code: "PVADLIC012" },
                "source-io" => e is IOException,
                _ => e is DirectoryNotFoundException,
            }) { Check(error == "file-io" ? !File.Exists(path) : File.ReadAllText(path) == "sentinel", name + ":original-failure-and-no-output"); }
            Denied(() => workbook.Save(path), name + ":failed-admission-closed");
        }

        PvadLicense.LoadFromFile(licenses[0]);
        var styleBook = new PvadWorkbook(); var styleCell = styleBook.AddSheet("Data").Cell("A1");
        Denied(() => styleCell.Style(style => { PvadLicense.LoadFromFile(licenses[1]); style.Bold(); }), "style:callback-native-mutation-stays-fresh");
        PvadLicense.LoadFromFile(licenses[0]);
        return checks.Count - start;

        void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException("Spreadsheet continuation failed: " + name); checks.Add("sheet-continuation:" + name); }
        void Denied(Action action, string name)
        {
            try { action(); } catch (PvadLicenseException e) when (e.Code == "PVADLIC012") { Check(true, name); return; }
            throw new InvalidOperationException("Expected fresh Spreadsheet denial: " + name);
        }
    }

    private sealed class CallbackValue(Action callback)
    {
        private bool called;
        public override string ToString() { if (!called) { called = true; callback(); } return "Synthetic continuation value"; }
    }
}
