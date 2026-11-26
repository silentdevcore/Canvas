using System.Text;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.FileImporter;
using PVAD.Infrastructure.Converters;
using PVAD.Infrastructure.Spreadsheet;
using PVAD.Licensing;

if (args.Length != 2) return 64;
var checks = new List<string>();
var outputPath = Path.Combine(Path.GetTempPath(), "pvad-account-denial-" + Guid.NewGuid().ToString("N") + ".csv");
try
{
    PvadLicense.LoadFromFile(args[0]);
    Require(new SvgDocumentExporter().Export(new DesignExportDto()).Length > 0, "legacy-svg-export");
    PvadLicense.LoadFromFile(args[1]);
    using var markdown = new MemoryStream(Encoding.UTF8.GetBytes("# Signed scope probe"));
    var design = MarkdownFileImporter.Import(markdown, "synthetic");
    Require(design.Pages.Count > 0, "scoped-markdown-import");
    Require(new OdtDocumentExporter().Export(design).Length > 0, "scoped-odt-export");
    var pdf = PVAD.Generator.Pdf.CreateDocument();
    pdf.AddPage(100, 100);
    Require(pdf.ToBytes().Length > 0, "scoped-pdf-output");
    Reject(() => pdf.ToBytes(new PVAD.Pdf.PdfSaveOptions { ComplianceProfile = PVAD.Pdf.PdfComplianceProfile.PdfA2B }),
        "scoped-archival-output-denied");
    Reject(() => ImageFileImporter.Import(null!, "synthetic"), "scoped-image-import-denied");
    Reject(() => SvgFileImporter.Import(null!, "synthetic"), "scoped-svg-import-denied");
    Reject(() => new ImageDocumentExporter().Export(null!), "scoped-png-export-denied");
    Reject(() => new JpegDocumentExporter().Export(null!), "scoped-jpeg-export-denied");
    Reject(() => new TiffDocumentExporter().Export(null!), "scoped-tiff-export-denied");
    Reject(() => new SvgDocumentExporter().Export(null!), "scoped-svg-export-denied");
    Reject(() => new HtmlDocumentExporter().Export(null!), "scoped-html-export-denied");
    Reject(() => new XmlDocumentExporter().Export(null!), "scoped-xml-export-denied");
    Reject(() => CsvSheetIo.ToCsv(new SheetDto()), "scoped-spreadsheet-csv-denied");
    Reject(() => CsvSheetIo.ToCsv(new SheetDto(), '\t'), "scoped-spreadsheet-tsv-denied");
    var workbook = new PvadWorkbook();
    Reject(() => workbook.Save(outputPath), "scoped-empty-workbook-save-denied");
    Require(!File.Exists(outputPath), "no-output-file-on-denial");
    Reject(() => PVAD.Generator.EInvoice.GenerateXml(null!, null!), "scoped-einvoice-generation-denied");
    Reject(() => PVAD.Generator.EInvoice.Validate(ReadOnlyMemory<byte>.Empty, null!), "scoped-einvoice-validation-denied");
    try
    {
        await FileImporterRegistry.Create("pdf").ImportAsync(null!, "synthetic");
        throw new InvalidOperationException("PDF import was not denied.");
    }
    catch (PvadLicenseException error) when (error.Code == "PVADLIC016") { checks.Add("scoped-pdf-import-denied"); }
    Console.WriteLine(JsonSerializer.Serialize(new { checks, syntheticOnly = true, networkActivationRequired = false }));
    return 0;
}
finally { if (File.Exists(outputPath)) File.Delete(outputPath); }

void Require(bool condition, string check)
{
    if (!condition) throw new InvalidOperationException("Capability probe failed: " + check);
    checks.Add(check);
}

void Reject(Action action, string check)
{
    try { action(); }
    catch (PvadLicenseException error) when (error.Code == "PVADLIC016") { checks.Add(check); return; }
    throw new InvalidOperationException("Expected capability rejection: " + check);
}
