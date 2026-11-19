using System.Reflection;
using System.Text.Json;
using PVAD.Core.Abstractions;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Spreadsheet;
using PVAD.Licensing;

if (args.Length != 3) return 64;
var checks = new List<string>();
var directory = Directory.CreateTempSubdirectory("pvad-spreadsheet-admission-").FullName;
try
{
    var dto = new SpreadsheetDto
    {
        Id = "native-sheet", Name = "Synthetic workbook", Sheets = [new SheetDto
        {
            Id = "sheet-1", Name = "Data", Cells =
            [
                new CellDto { Row = 0, Col = 0, Type = "number", Value = 3 },
                new CellDto { Row = 1, Col = 0, Type = "number", Value = 1 },
                new CellDto { Row = 2, Col = 0, Type = "formula", Formula = "=SUM(A1:A2)", Value = -77 },
                new CellDto { Row = 0, Col = 1, Type = "text", Value = "{{amount}}" },
            ],
        }],
    };
    var design = new DesignExportDto { Name = "Spreadsheet table", Pages = [new PageDto
    {
        Id = "p1", Elements = [new ElementDto { Id = "t1", Type = "table", CellData = [["Item", "Amount"], ["A", "3"]] }],
    }] };
    var exporter = new ExcelWorkbookExporter();
    var importer = new ExcelWorkbookImporter();
    var xls = new XlsWorkbookIo();
    var calculator = new SpreadsheetCalculator();
    var bridge = new SpreadsheetToDesignConverter();
    var operations = new SpreadsheetOperations();
    var data = new SpreadsheetData();
    var documentExporter = new ExcelDocumentExporter();
    var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{\"amount\":42}")!;
    byte[] xlsxBytes = [1], xlsBytes = [1];
    var denying = true;
    var admissions = new Dictionary<string, Action>();
    var free = new HashSet<string>();

    Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "initially-unlicensed");
    Check(documentExporter.FormatKey == "excel" && documentExporter.FileExtension == ".xlsx", "free-export-metadata");
    Check(new SheetRendererCapabilities().SupportsHeaderFooter, "free-renderer-metadata");
    Check(new SpreadsheetValidator().Validate(dto).Valid, "free-structural-validation");

    Add<ExcelWorkbookExporter>("Export", [typeof(SpreadsheetDto), typeof(bool)], () => exporter.Export(dto, true));
    Add<ExcelWorkbookImporter>("Import", [typeof(Stream), typeof(string)], () => Read(xlsxBytes, stream => importer.Import(stream)));
    Add<ExcelDocumentExporter>("Export", [typeof(DesignExportDto)], () => documentExporter.Export(design));
    Add<SpreadsheetCalculator>("Calculate", [typeof(SpreadsheetDto)], () => calculator.Calculate(dto));
    Add<XlsWorkbookIo>("Export", [typeof(SpreadsheetDto)], () => xls.Export(dto));
    Add<XlsWorkbookIo>("Import", [typeof(Stream), typeof(string)], () => Read(xlsBytes, stream => xls.Import(stream)));
    // Static classes cannot be generic type arguments; register their exact methods directly.
    admissions.Add(Key(typeof(CsvSheetIo).GetMethod(nameof(CsvSheetIo.ToCsv))!), () => CsvSheetIo.ToCsv(dto.Sheets[0]));
    admissions.Add(Key(typeof(CsvSheetIo).GetMethod(nameof(CsvSheetIo.FromCsv))!), () => CsvSheetIo.FromCsv("A,3"));
    Add<SpreadsheetToDesignConverter>("Convert", [typeof(SpreadsheetDto), typeof(int), typeof(bool)], () => bridge.Convert(dto));
    Add<SpreadsheetOperations>("SortRange", [typeof(SheetDto), typeof(string), typeof(int), typeof(bool)], () => operations.SortRange(dto.Sheets[0], "A1:A2", 0));
    Add<SpreadsheetOperations>("FindReplace", [typeof(SpreadsheetDto), typeof(string), typeof(string), typeof(bool)], () => operations.FindReplace(dto, "amount", "amount"));
    Add<SpreadsheetData>("FromRows", [typeof(IReadOnlyList<Dictionary<string, JsonElement>>), typeof(string)], () => data.FromRows([values]));
    Add<SpreadsheetData>("Fill", [typeof(SpreadsheetDto), typeof(Dictionary<string, JsonElement>)], () => data.Fill(dto, values));
    foreach (var (name, action) in admissions) Denied(action, "PVADLIC001", "missing:" + name);
    Denied(() => new PvadWorkbook(), "PVADLIC001", "missing:workbook-constructor");
    Denied(() => PVAD.Generator.Spreadsheet.CreateWorkbook(), "PVADLIC001", "missing:generator-facade");
    Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "expired-load-rejected");
    Denied(() => new PvadWorkbook(), "PVADLIC001", "expired-load-does-not-activate");

    PvadLicense.LoadFromFile(args[0]);
    denying = false;
    foreach (var product in new[] { PvadLicenseProducts.Pdf, PvadLicenseProducts.Word, PvadLicenseProducts.Importer, PvadLicenseProducts.Converter })
        Denied(() => PvadLicense.Require(product), "PVADLIC012", "spreadsheet-only-without:" + product);
    xlsxBytes = exporter.Export(dto, true);
    xlsBytes = xls.Export(dto);
    Check(xlsxBytes.AsSpan().StartsWith("PK"u8), "xlsx-signature");
    Check(xlsBytes.AsSpan().StartsWith(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 }), "xls-signature");
    Read(xlsxBytes, stream => Check(importer.Import(stream).Sheets[0].Cells.Any(c => c.Formula == "=SUM(A1:A2)"), "xlsx-formula-roundtrip"));
    Read(xlsBytes, stream => Check(xls.Import(stream).Sheets[0].Cells.Any(c => c.Formula == "=SUM(A1:A2)"), "xls-formula-roundtrip"));
    Check(Convert.ToDouble(calculator.Calculate(dto).Sheets[0].Cells.Single(c => c.Type == "formula").Value) == 4, "calculated-value");
    Check(CsvSheetIo.FromCsv(CsvSheetIo.ToCsv(dto.Sheets[0])).Cells.Count > 0, "csv-roundtrip");
    Check(CsvSheetIo.FromCsv(CsvSheetIo.ToCsv(dto.Sheets[0], '\t'), delimiter: '\t').Cells.Count > 0, "tsv-roundtrip");
    Check(bridge.Convert(dto).Pages[0].Elements[0].Type == "table", "design-bridge");
    Check(((IDocumentExporter)documentExporter).Export(design, new ExportOptions()).Length > 0, "interface-design-export");

    var workbook = PVAD.Generator.Spreadsheet.CreateWorkbook("Fluent workbook");
    var emptyWorkbook = new PvadWorkbook("Empty workbook");
    var sheet = workbook.AddSheet("Data");
    var cell = sheet.Cell("A1").Value(7);
    PvadCellStyle? capturedStyle = null;
    cell.Style(style => capturedStyle = style);
    var style = capturedStyle!;
    var column = sheet.Column(0);
    var range = sheet.Range("A1:B1");
    var callbackCalled = false;
    Add<PvadWorkbook>(".ctor", [typeof(string)], () => new PvadWorkbook());
    Add<PvadWorkbook>("AddSheet", [typeof(string)], () => workbook.AddSheet("Another"));
    Add<PvadWorkbook>("DefineName", [typeof(string), typeof(string)], () => workbook.DefineName("Amounts", "Data!$A$1"));
    Add<PvadWorkbook>("ToXlsx", [typeof(bool)], () => workbook.ToXlsx());
    Add<PvadWorkbook>("Save", [typeof(string)], () => workbook.Save(Path.Combine(directory, "saved.xlsx")));
    Add<PvadWorksheet>("Cell", [typeof(int), typeof(int)], () => sheet.Cell(5, 5));
    Add<PvadWorksheet>("Cell", [typeof(string)], () => sheet.Cell("G7"));
    Add<PvadWorksheet>("Merge", [typeof(string)], () => sheet.Merge("A1:B1"));
    Add<PvadWorksheet>("Column", [typeof(int)], () => sheet.Column(4));
    Add<PvadWorksheet>("Freeze", [typeof(int), typeof(int)], () => sheet.Freeze(1, 1));
    Add<PvadRange>("Merge", [], () => range.Merge());
    Add<PvadColumn>("Width", [typeof(double)], () => column.Width(20));
    Add<PvadColumn>("Hidden", [typeof(bool)], () => column.Hidden());
    Add<PvadColumn>("OutlineLevel", [typeof(int)], () => column.OutlineLevel(1));
    Add<PvadCell>("Value", [typeof(object)], () => cell.Value(42));
    Add<PvadCell>("Formula", [typeof(string)], () => cell.Formula("=1+2"));
    Add<PvadCell>("NumberFormat", [typeof(string)], () => cell.NumberFormat("0.00"));
    Add<PvadCell>("Comment", [typeof(string)], () => cell.Comment("Synthetic comment"));
    Add<PvadCell>("Hyperlink", [typeof(string)], () => cell.Hyperlink("https://example.com"));
    Add<PvadCell>("Style", [typeof(Action<PvadCellStyle>)], () => cell.Style(_ => callbackCalled = true));
    Add<PvadCellStyle>("Bold", [typeof(bool)], () => style.Bold());
    Add<PvadCellStyle>("Italic", [typeof(bool)], () => style.Italic());
    Add<PvadCellStyle>("Background", [typeof(string)], () => style.Background("#ffffff"));
    Add<PvadCellStyle>("Color", [typeof(string)], () => style.Color("#000000"));
    Add<PvadCellStyle>("Font", [typeof(string)], () => style.Font("Arial"));
    Add<PvadCellStyle>("FontSize", [typeof(double)], () => style.FontSize(14));
    Add<PvadCellStyle>("Align", [typeof(string)], () => style.Align("center"));

    Free<PvadWorkbook>("Sheet", [typeof(int)]);
    Free<PvadWorkbook>("Sheet", [typeof(string)]);
    Free<PvadWorkbook>("ToWorkbook", []);
    Free<PvadWorksheet>("Range", [typeof(string)]);
    var freeTypes = new[] { typeof(SheetRendererCapabilities), typeof(SpreadsheetValidator), typeof(SpreadsheetValidationIssue), typeof(SpreadsheetValidationResult) };
    var paidTypes = new[] { typeof(PvadWorkbook), typeof(PvadWorksheet), typeof(PvadRange), typeof(PvadColumn), typeof(PvadCell), typeof(PvadCellStyle), typeof(ExcelWorkbookExporter), typeof(ExcelWorkbookImporter), typeof(ExcelDocumentExporter), typeof(XlsWorkbookIo), typeof(CsvSheetIo), typeof(SpreadsheetCalculator), typeof(SpreadsheetOperations), typeof(SpreadsheetData), typeof(SpreadsheetToDesignConverter) };
    var publicTypes = typeof(PvadWorkbook).Assembly.GetExportedTypes();
    Check(publicTypes.OrderBy(t => t.FullName).SequenceEqual(paidTypes.Concat(freeTypes).OrderBy(t => t.FullName)), "public-type-inventory");
    var methods = paidTypes.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).Where(m => !m.IsSpecialName).Select(Key).ToHashSet();
    Check(methods.SetEquals(admissions.Keys.Where(key => !key.Contains(".ctor(")).Concat(free)), "public-method-inventory");
    var freeConstructors = new[] { typeof(ExcelWorkbookExporter), typeof(ExcelWorkbookImporter), typeof(ExcelDocumentExporter), typeof(XlsWorkbookIo), typeof(SpreadsheetCalculator), typeof(SpreadsheetOperations), typeof(SpreadsheetData), typeof(SpreadsheetToDesignConverter) }
        .Select(type => Key(type.GetConstructor(Type.EmptyTypes)!));
    var constructors = paidTypes.SelectMany(type => type.GetConstructors()).Select(Key).ToHashSet();
    Check(constructors.SetEquals(admissions.Keys.Where(key => key.Contains(".ctor(")).Concat(freeConstructors)), "public-constructor-inventory");
    var properties = paidTypes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)).Select(p => p.DeclaringType!.Name + "." + p.Name).ToHashSet();
    Check(properties.SetEquals(["PvadWorkbook.Name", "PvadWorkbook.SheetCount", "PvadWorksheet.Name", "PvadCell.Sheet", "ExcelDocumentExporter.FormatKey", "ExcelDocumentExporter.MimeType", "ExcelDocumentExporter.FileExtension", "ExcelDocumentExporter.Capabilities"]), "public-property-inventory");

    foreach (var (name, action) in admissions) { action(); checks.Add("valid:" + name); }
    foreach (var extension in new[] { "xlsx", "xls", "csv", "tsv" }) workbook.Save(Path.Combine(directory, "saved." + extension));
    var beforeDto = JsonSerializer.Serialize(dto);
    var beforeBuilder = JsonSerializer.Serialize(workbook.ToWorkbook());
    var beforeDesign = JsonSerializer.Serialize(design);
    var files = Directory.GetFiles(directory).ToDictionary(path => path, File.ReadAllBytes);
    callbackCalled = false;
    PvadLicense.LoadFromFile(args[1]);
    denying = true;
    foreach (var (name, action) in admissions) Denied(action, "PVADLIC012", "wrong:" + name);
    Denied(() => PVAD.Generator.Spreadsheet.CreateWorkbook(), "PVADLIC012", "wrong:generator-facade");
    Check(!callbackCalled, "denied-style-does-not-invoke-callback");
    foreach (var extension in new[] { "csv", "tsv" })
    {
        var emptyPath = Path.Combine(directory, "empty." + extension);
        Denied(() => emptyWorkbook.Save(emptyPath), "PVADLIC012", "empty-builder-save-denied:" + extension);
        Check(!File.Exists(emptyPath), "empty-builder-file-not-created:" + extension);
    }
    foreach (var extension in new[] { "xlsx", "xls", "csv", "tsv" })
    {
        var path = Path.Combine(directory, "saved." + extension);
        Denied(() => workbook.Save(path), "PVADLIC012", "denied-existing-file:" + extension);
        Check(File.ReadAllBytes(path).SequenceEqual(files[path]), "unchanged-file:" + extension);
        var absent = Path.Combine(directory, "absent." + extension);
        Denied(() => workbook.Save(absent), "PVADLIC012", "denied-new-file:" + extension);
        Check(!File.Exists(absent), "absent-file-not-created:" + extension);
    }
    Check(JsonSerializer.Serialize(dto) == beforeDto && JsonSerializer.Serialize(design) == beforeDesign && JsonSerializer.Serialize(workbook.ToWorkbook()) == beforeBuilder, "denied-operations-preserve-models");
    Check(workbook.SheetCount > 0 && workbook.Sheet(0).Name == "Data", "read-only-builder-inspection-remains-free");
    Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "expired-renewal-rejected");
    Denied(() => exporter.Export(dto), "PVADLIC012", "failed-renewal-keeps-previous-snapshot");
    PvadLicense.LoadFromFile(args[0]);
    Check(workbook.ToXlsx().Length > 0, "renewal-restores-existing-builder");
    var continuationChecks = SpreadsheetContinuationChecks.Run(args, checks, directory);
    Console.WriteLine(JsonSerializer.Serialize(new { checks, protectedMethods = admissions.Count, continuationChecks, processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
    return 0;

    void Add<T>(string name, Type[] parameters, Action action)
    {
        MethodBase member = name == ".ctor" ? typeof(T).GetConstructor(parameters)! : typeof(T).GetMethod(name, parameters)!;
        admissions.Add(Key(member), action);
    }
    void Free<T>(string name, Type[] parameters) => free.Add(Key(typeof(T).GetMethod(name, parameters)!));
    void Read(byte[] bytes, Action<Stream> read)
    {
        using var stream = new MemoryStream(bytes);
        try { read(stream); }
        finally
        {
            if (denying && (!stream.CanRead || stream.Position != 0 || !stream.ToArray().SequenceEqual(bytes)))
                throw new InvalidOperationException("Denied import changed its source stream.");
        }
    }
}
finally { Directory.Delete(directory, recursive: true); }

string Key(MethodBase method) => $"{method.DeclaringType!.Name}.{method.Name}({string.Join(',', method.GetParameters().Select(p => p.ParameterType.ToString()))})";
void Check(bool valid, string name)
{
    if (!valid) throw new InvalidOperationException($"Probe failed: {name}");
    checks.Add(name);
}
void Denied(Action action, string code, string name)
{
    try { action(); }
    catch (PvadLicenseException exception) when (exception.Code == code) { checks.Add(name); return; }
    throw new InvalidOperationException($"Probe expected {code}: {name}");
}
