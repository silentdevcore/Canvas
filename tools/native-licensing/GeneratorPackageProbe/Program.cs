using System.IO.Compression;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using PVAD.Core.Abstractions;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Pdf;
using PVAD.Infrastructure.Spreadsheet;
using PVAD.Licensing;

if (args.Length is not (5 or 6)) return 64;
var checks = new List<string>();
void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
void Denied(Action action, string code, string name)
{
    try { action(); throw new InvalidOperationException(name); }
    catch (PvadLicenseException error) { Check(error.Code == code, name); }
}
var output = args[4];
Directory.CreateDirectory(output);
Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "no-implicit-activation");
var design = new DesignExportDto
{
    Id = "package-design", Name = "Synthetic package document",
    PageSettings = new PageSettingsDto { Width = 595, Height = 842 },
    Pages = [new PageDto { Id = "p1", Elements =
        [new ElementDto { Id = "text1", Type = "text", Content = "Synthetic package content", X = 30, Y = 40, Width = 300, Height = 30 }] }],
};
var json = JsonSerializer.Serialize(design);
var restored = JsonSerializer.Deserialize<DesignExportDto>(json)!;
Check(JsonSerializer.Serialize(restored) == json, "public-dto-json-roundtrip");
var unlicensedCoreDtoTypes = FreeContractChecks.Run(restored, checks);
Denied(() => PVAD.Generator.Pdf.CreateDocument(), "PVADLIC001", "unlicensed-pdf-denied");
Denied(() => PVAD.Generator.Word.Export(design), "PVADLIC001", "unlicensed-word-denied");
Denied(() => PVAD.Generator.Spreadsheet.CreateWorkbook(), "PVADLIC001", "unlicensed-spreadsheet-denied");
#if PVAD_IMPORTER_PACKAGE_PROBE
await ImporterPackageChecks.MissingAsync(checks);
#endif
#if PVAD_EXECUTION_PACKAGE_PROBE
ExecutionPackageChecks.Missing(checks);
#endif
PvadLicense.LoadFromFile(args[1]);
Denied(() => PVAD.Generator.Pdf.CreateDocument(), "PVADLIC012", "wrong-product-pdf-denied");
Denied(() => PVAD.Generator.Word.Export(design), "PVADLIC012", "wrong-product-word-denied");
Denied(() => PVAD.Generator.Spreadsheet.CreateWorkbook(), "PVADLIC012", "wrong-product-spreadsheet-denied");
PvadLicense.LoadFromFile(Environment.GetEnvironmentVariable("PVAD_PACKAGE_PDF_LICENSE_PATH")!);
ProductGrantChecks.Only("pdf", restored, checks);
var document = PVAD.Generator.Pdf.CreateDocument();
document.AddPage(595, 842).DrawText("Synthetic package content", 40, 800, 18);
document.AddPage(842, 595).DrawText("Landscape page", 40, 540, 18);
IDocumentRenderer renderer = new PdfDocumentRenderer();
var pdf = renderer.Render(document);
Check(pdf.AsSpan().StartsWith("%PDF-"u8), "pdf-facade-interface-renderer");
File.WriteAllBytes(Path.Combine(output, "document.pdf"), pdf);
PvadLicense.LoadFromFile(Environment.GetEnvironmentVariable("PVAD_PACKAGE_WORD_LICENSE_PATH")!);
ProductGrantChecks.Only("word", restored, checks);
Denied(() => document.ToBytes(), "PVADLIC012", "single-word-retained-pdf-denied");
var word = PVAD.Generator.Word.Export(restored);
using var zip = new ZipArchive(new MemoryStream(word));
using var wordStream = zip.GetEntry("word/document.xml")!.Open();
var wordXml = XDocument.Load(wordStream);
XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
Check(wordXml.Descendants(w + "t").Any(item => item.Value == "Synthetic package content"), "docx-content-preserved");
PvadLicense.LoadFromFile(Environment.GetEnvironmentVariable("PVAD_PACKAGE_SPREADSHEET_LICENSE_PATH")!);
ProductGrantChecks.Only("spreadsheet", restored, checks);
var workbook = PVAD.Generator.Spreadsheet.CreateWorkbook("Synthetic workbook");
var sheet = workbook.AddSheet("Data");
sheet.Cell("A1").Value(3).Style(style => style.Bold().Color("#007766"));
sheet.Cell("A2").Value(1);
sheet.Cell("A3").Formula("=SUM(A1:A2)");
var xlsx = workbook.ToXlsx();
Check(xlsx.AsSpan().StartsWith("PK"u8), "xlsx-package-generated");
using var source = new MemoryStream(xlsx);
var imported = new ExcelWorkbookImporter().Import(source);
Check(source.CanRead, "xlsx-source-stream-left-open");
Check(imported.Sheets[0].Cells.Any(cell => cell.Formula == "=SUM(A1:A2)"), "xlsx-formula-roundtrip");
var calculated = new SpreadsheetCalculator().Calculate(imported);
var formula = calculated.Sheets[0].Cells.Single(cell => cell.Type == "formula");
Check(Convert.ToDouble(formula.Value) == 4, "xlsx-formula-calculation");
var resources = typeof(DesignExportDto).Assembly.GetManifestResourceNames();
Check(resources.Contains("PVAD.DesignerDateFormatting.json"), "core-embedded-formatting-resource");
Check(typeof(PvadLicense).GetMethod(nameof(PvadLicense.LoadFromStream)) is not null, "public-reflection-entry");
Check(typeof(PvadLicense).Assembly.GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
    .Any(item => item.AssemblyName == "PVAD.Pdf"), "friend-assembly-metadata");
PvadLicense.LoadFromFile(args[2]);
PvadLicense.ImportRevocationsFromFile(args[3]);
Check(PvadLicense.GetStatus().State == PvadLicenseState.Revoked, "protected-revocation-status");
Denied(() => document.ToBytes(), "PVADLIC014", "retained-pdf-rechecks-revocation");
Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC014", "revoked-reload-denied");
PvadLicense.LoadFromFile(args[0]);
Check(PvadLicense.GetRevocationStatus().RevokedLicenseCount == 2, "renewal-retains-revocations");
object? importerSemantics = null;
object? executionSemantics = null;
#if PVAD_IMPORTER_PACKAGE_PROBE
importerSemantics = await ImporterPackageChecks.LicensedAsync(args, checks);
#endif
#if PVAD_EXECUTION_PACKAGE_PROBE
executionSemantics = await ExecutionPackageChecks.LicensedAsync(args, checks);
#endif
var assemblies = new List<string> { "PVAD.Licensing", "PVAD.Core", "PVAD.Domain", "PVAD.Application", "PVAD.Pdf", "PVAD.Infrastructure.Pdf", "PVAD.Infrastructure.Word", "PVAD.Infrastructure.Spreadsheet", "PVAD.Generator" };
#if PVAD_IMPORTER_PACKAGE_PROBE
assemblies.AddRange(["PVAD.Importer", "PVAD.FileImporter", "PVAD.FileImporter.ImageAnalysis", "PVAD.FileImporter.ImageOcr"]);
#endif
#if PVAD_EXECUTION_PACKAGE_PROBE
assemblies.Add("PVAD.Infrastructure.Converters");
#endif
#if PVAD_MIGRATION_PACKAGE_PROBE
assemblies.AddRange(JsonSerializer.Deserialize<string[]>(Environment.GetEnvironmentVariable("PVAD_PACKAGE_MIGRATION_ASSEMBLIES")!)!);
#endif
var assemblyHashes = new Dictionary<string, string>();
foreach (var name in assemblies)
{
    var assembly = Assembly.Load(name);
    using var file = File.OpenRead(assembly.Location);
    using var pe = new PEReader(file);
    Check(!pe.ReadDebugDirectory().Any(entry => entry.Type is DebugDirectoryEntryType.CodeView or DebugDirectoryEntryType.EmbeddedPortablePdb), "no-private-debug:" + name);
    file.Position = 0;
    assemblyHashes.Add(name, Convert.ToHexStringLower(SHA256.HashData(file)));
}
var inspectedNativePackages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
if (args.Length == 6)
{
    foreach (var name in assemblies)
    {
        var packagePath = Path.Combine(args[5], name + "." + typeof(PvadLicense).Assembly.GetName().Version!.ToString(3) + ".nupkg");
        using var package = ZipFile.OpenRead(packagePath);
        foreach (var entry in package.Entries)
        {
            Check(!new[] { ".cs", ".pdb", ".pem", ".pfx", ".snk", ".map", ".snupkg" }
                .Any(extension => entry.FullName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)), "package-no-private-file:" + name + ":" + entry.FullName);
            using var content = entry.Open();
            using var bytes = new MemoryStream(); content.CopyTo(bytes);
            var text = Encoding.UTF8.GetString(bytes.ToArray());
            Check(!text.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal) && !text.Contains("-----BEGIN EC PRIVATE KEY-----", StringComparison.Ordinal), "package-no-private-key:" + name + ":" + entry.FullName);
        }
        var dlls = package.Entries.Where(entry => entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(dlls.Length == 1 && dlls[0].FullName == "lib/net10.0/" + name + ".dll", "package-exact-library:" + name);
        using var packaged = dlls[0].Open();
        Check(Convert.ToHexStringLower(SHA256.HashData(packaged)) == assemblyHashes[name], "package-consumer-exact-hash:" + name);
        Check(package.Entries.Any(entry => entry.FullName == "lib/net10.0/" + name + ".xml"), "package-xml-api-documentation:" + name);
        using var documentationManifestStream = package.GetEntry("docs/documentation-manifest.json")!.Open();
        using var documentationManifest = JsonDocument.Parse(documentationManifestStream);
        var bundle = documentationManifest.RootElement;
        Check(bundle.GetProperty("package").GetString() == name, "package-documentation-identity:" + name);
        Check(!bundle.GetProperty("publicationReady").GetBoolean() && !bundle.GetProperty("legalApprovalGranted").GetBoolean(), "package-documentation-no-legal-approval:" + name);
        Check(bundle.GetProperty("xml").GetProperty("assemblySha256").GetString() == assemblyHashes[name], "package-documentation-bound-to-dll:" + name);
        foreach (var expected in bundle.GetProperty("files").EnumerateObject())
        {
            var entry = package.GetEntry(expected.Name);
            Check(entry is not null, "package-documentation-file:" + name + ":" + expected.Name);
            using var bytes = entry!.Open();
            Check(Convert.ToHexStringLower(SHA256.HashData(bytes)) == expected.Value.GetString(), "package-documentation-checksum:" + name + ":" + expected.Name);
        }
        foreach (var native in bundle.GetProperty("nativePackages").EnumerateArray())
        {
            var identity = native.GetProperty("id").GetString()! + "/" + native.GetProperty("version").GetString()!;
            var evidenceHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(native.GetRawText())));
            if (inspectedNativePackages.TryGetValue(identity, out var previous))
            {
                Check(previous == evidenceHash, "native-evidence-consistent:" + name + ":" + identity);
                continue;
            }
            inspectedNativePackages.Add(identity, evidenceHash);
            var inspection = native.GetProperty("inspection");
            Check(!native.GetProperty("legalApprovalGranted").GetBoolean() && !native.GetProperty("publicationReady").GetBoolean() &&
                inspection.GetProperty("status").GetString() == "archive-and-cache-verified" &&
                !inspection.GetProperty("nativeComponentsReviewed").GetBoolean() &&
                inspection.GetProperty("archiveAuthenticity").GetString() == "not-verified", "native-evidence-no-approval:" + identity);
            var dependencyArchive = Path.Combine(Path.GetDirectoryName(args[5])!, "third-party-feed", identity.Replace('/', '.').ToLowerInvariant() + ".nupkg");
            using (var dependencyBytes = File.OpenRead(dependencyArchive))
                Check(Convert.ToHexStringLower(SHA256.HashData(dependencyBytes)) == inspection.GetProperty("archiveSha256").GetString(), "native-dependency-archive:" + identity);
            using var dependency = ZipFile.OpenRead(dependencyArchive);
            foreach (var asset in inspection.GetProperty("assets").EnumerateArray())
            {
                var fileName = asset.GetProperty("file").GetString()!;
                Check(!asset.GetProperty("architectureVerified").GetBoolean() &&
                    asset.GetProperty("deploymentSelection").GetString() == "not-evaluated" &&
                    asset.GetProperty("componentAttribution").GetString() == "review-required", "native-declaration-not-platform-approval:" + identity + ":" + fileName);
                var entry = dependency.GetEntry(fileName);
                Check(entry is not null && entry.Length == asset.GetProperty("bytes").GetInt64() && entry.Length <= 128L * 1024 * 1024,
                    "native-dependency-entry:" + identity + ":" + fileName);
                using var bytes = entry!.Open();
                Check(Convert.ToHexStringLower(SHA256.HashData(bytes)) == asset.GetProperty("sha256").GetString(), "native-dependency-payload:" + identity + ":" + fileName);
            }
            foreach (var notice in native.GetProperty("noticeFiles").EnumerateArray())
            {
                var noticePath = "docs/third-party/" + notice.GetProperty("file").GetString();
                var entry = package.GetEntry(noticePath);
                Check(entry is not null, "native-associated-notice:" + identity + ":" + noticePath);
                using var bytes = entry!.Open();
                Check(Convert.ToHexStringLower(SHA256.HashData(bytes)) == notice.GetProperty("sha256").GetString(), "native-associated-notice-bytes:" + identity + ":" + noticePath);
            }
        }
        if (name == "PVAD.FileImporter.ImageOcr")
        {
            using var specification = package.GetEntry(name + ".nuspec")!.Open();
            var declarations = XDocument.Load(specification).Descendants().Where(element => element.Name.LocalName == "files").ToArray();
            foreach (var expected in bundle.GetProperty("files").EnumerateObject().Where(item => item.Name.StartsWith("docs/runtime-assets/", StringComparison.Ordinal)))
            {
                var relative = expected.Name["docs/runtime-assets/".Length..];
                var contentPath = "any/net10.0/third-party/runtime-assets/ocr/" + relative;
                var entry = package.GetEntry("contentFiles/" + contentPath);
                Check(entry is not null, "package-runtime-notice-path:" + relative);
                using var bytes = entry!.Open();
                Check(Convert.ToHexStringLower(SHA256.HashData(bytes)) == expected.Value.GetString(), "package-runtime-notice-bytes:" + relative);
                var declaration = declarations.SingleOrDefault(element => ((string?)element.Attribute("include"))?.Replace('\\', '/') == contentPath);
                Check((string?)declaration?.Attribute("copyToOutput") == "true" && (string?)declaration?.Attribute("flatten") == "false", "package-runtime-notice-copy-contract:" + relative);
                var copied = Path.Combine(AppContext.BaseDirectory, "third-party", "runtime-assets", "ocr", relative.Replace('/', Path.DirectorySeparatorChar));
                Check(File.Exists(copied), "consumer-runtime-notice-exists:" + relative);
                using var copiedBytes = File.OpenRead(copied);
                Check(Convert.ToHexStringLower(SHA256.HashData(copiedBytes)) == expected.Value.GetString(), "consumer-runtime-notice-bytes:" + relative);
            }
        }
    }
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    checks, assemblyHashes, unlicensedCoreDtoTypes, nativePackagesInspected = inspectedNativePackages.Count,
    singleProductProfiles = ProductGrantChecks.Profiles.Order(StringComparer.Ordinal).ToArray(),
    processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
    semantics = new { json, wordXml = wordXml.ToString(SaveOptions.DisableFormatting), importer = importerSemantics, execution = executionSemantics,
        spreadsheet = JsonSerializer.Serialize(calculated), resources = resources.Order(StringComparer.Ordinal).ToArray() },
}));
return 0;
