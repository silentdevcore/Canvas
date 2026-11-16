using System.Text;
using System.Text.Json;
using PVAD.Licensing;
using PVAD.Pdf;

if (args.Length != 3) return 64;
var checks = new List<string>();
var directory = Directory.CreateTempSubdirectory("pvad-pdf-admission-").FullName;
try
{
    _ = new PdfDocumentInfo { Title = "Free metadata" };
    _ = new PdfSaveOptions();
    Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "initially-unlicensed");
    Denied(() => new PdfDocument(), "PVADLIC001", "direct-construction-denied");
    Denied(() => PVAD.Generator.Pdf.CreateDocument(), "PVADLIC001", "facade-construction-denied");

    PvadLicense.LoadFromFile(args[0]);
    var document = PVAD.Generator.Pdf.CreateDocument();
    var emptyDocument = new PdfDocument();
    document.AddPage().DrawText("Licensed native PDF", 40, 800, 18);
    var bytes = document.ToBytes();
    Check(Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-", "licensed-byte-export");
    using var output = new MemoryStream();
    document.Save(output);
    Check(output.Length > 5 && output.CanWrite, "licensed-stream-export-leaves-open");
    var file = Path.Combine(directory, "document.pdf");
    document.Save(file);
    Check(File.ReadAllBytes(file).AsSpan(0, 5).SequenceEqual("%PDF-"u8), "licensed-file-export");

    // A previously admitted document cannot start another export after renewal
    // removes its product. No private authority reset or testing bypass is used.
    PvadLicense.LoadFromFile(args[1]);
    Denied(() => new PdfDocument(), "PVADLIC012", "wrong-product-construction-denied");
    Denied(() => document.ToBytes(), "PVADLIC012", "byte-export-rechecks-product");
    Denied(() => emptyDocument.ToBytes(), "PVADLIC012", "empty-export-rechecks-product");
    Check(emptyDocument.Pages.Count == 0, "denied-export-does-not-add-an-empty-page");
    var originalFile = File.ReadAllBytes(file);
    Denied(() => document.Save(file), "PVADLIC012", "file-export-rechecks-product");
    Check(originalFile.SequenceEqual(File.ReadAllBytes(file)), "denied-export-preserves-existing-file");
    var absentFile = Path.Combine(directory, "absent.pdf");
    Denied(() => document.Save(absentFile), "PVADLIC012", "new-file-export-denied");
    Check(!File.Exists(absentFile), "denied-export-does-not-create-file");
    var originalStream = output.ToArray();
    var position = output.Position;
    Denied(() => document.Save(output), "PVADLIC012", "stream-export-rechecks-product");
    Check(output.CanWrite && output.Position == position && originalStream.SequenceEqual(output.ToArray()),
        "denied-export-preserves-stream-and-position");
    Check(document.Pages.Count == 1, "denied-export-preserves-pages");
    Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "expired-license-rejected");
    Denied(() => document.ToBytes(), "PVADLIC012", "failed-renewal-preserves-previous-license");
    PvadLicense.LoadFromFile(args[0]);
    Check(document.ToBytes().Length > 5, "valid-renewal-restores-export-without-restart");
    Console.WriteLine(JsonSerializer.Serialize(new { checks, processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
    return 0;
}
finally { Directory.Delete(directory, recursive: true); }

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
