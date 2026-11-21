using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using PVAD.Core.Abstractions;
using PVAD.Infrastructure.Pdf;
using PVAD.Licensing;
using PVAD.Pdf;

if (args.Length != 3) return 64;
var checks = new List<string>();
var directory = Directory.CreateTempSubdirectory("pvad-pdf-admission-").FullName;
try
{
    PdfDocument? retained = null;
    var matrix = new NativeAdmissionMatrix();
    var constructor = typeof(PdfDocument).GetConstructor([typeof(PdfStandardFont)])!;
    var saveOptions = new PdfSaveOptions { CollectDiagnostics = true };
    var renderer = new PdfDocumentRenderer();
    var matrixFile = Path.Combine(directory, "matrix.pdf");
    File.WriteAllBytes(matrixFile, [4, 5, 6]);
    using var matrixStream = new MemoryStream();
    matrixStream.Write([7, 8, 9]);
    matrixStream.Position = 1;
    string Invariant() => JsonSerializer.Serialize(new
    {
        file = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(matrixFile))),
        stream = Convert.ToHexString(matrixStream.ToArray()), matrixStream.Position,
        matrixStream.CanWrite, pages = retained?.Pages.Count,
    });
    void PdfBytes(byte[] content)
    {
        if (!content.AsSpan().StartsWith("%PDF-"u8)) throw new InvalidOperationException("Not PDF bytes.");
    }
    matrix.Add(constructor, "direct", () => _ = new PdfDocument(), Invariant);
    matrix.Add(typeof(PVAD.Generator.Pdf).GetMethod("CreateDocument")!, "facade",
        () => _ = PVAD.Generator.Pdf.CreateDocument(), Invariant);
    void DocumentEntry(Type type, string name, Type[] parameters, string dispatch, Action invoke) =>
        matrix.Add(type.GetMethod(name, parameters)!, dispatch, invoke, Invariant,
            constructor, () => _ = new PdfDocument());
    DocumentEntry(typeof(PdfDocument), "ToBytes", [typeof(PdfSaveOptions)], "direct",
        () => PdfBytes(retained!.ToBytes(saveOptions)));
    DocumentEntry(typeof(PdfDocument), "Save", [typeof(string)], "direct",
        () => { retained!.Save(matrixFile); PdfBytes(File.ReadAllBytes(matrixFile)); });
    DocumentEntry(typeof(PdfDocument), "Save", [typeof(string), typeof(PdfSaveOptions)], "direct",
        () => { retained!.Save(matrixFile, saveOptions); PdfBytes(File.ReadAllBytes(matrixFile)); });
    DocumentEntry(typeof(PdfDocument), "Save", [typeof(Stream), typeof(PdfSaveOptions)], "direct",
        () => { var start = (int)matrixStream.Position; retained!.Save(matrixStream, saveOptions);
            PdfBytes(matrixStream.ToArray()[start..]); });
    DocumentEntry(typeof(PdfDocumentRenderer), "Render", [typeof(object)], "direct",
        () => PdfBytes(renderer.Render(retained!)));
    DocumentEntry(typeof(IDocumentRenderer), "Render", [typeof(object)], "interface",
        () => PdfBytes(((IDocumentRenderer)renderer).Render(retained!)));
    matrix.RequireSurface(typeof(PdfDocument), "ToBytes", "Save");
    matrix.RequireConstructors(typeof(PdfDocument));
    matrix.RequireSurface(typeof(PVAD.Generator.Pdf));
    matrix.RequireSurface(typeof(PdfDocumentRenderer));
    matrix.RequireSurface(typeof(IDocumentRenderer));
    _ = new PdfDocumentInfo { Title = "Free metadata" };
    _ = new PdfSaveOptions();
    Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "initially-unlicensed");
    Denied(() => new PdfDocument(), "PVADLIC001", "direct-construction-denied");
    Denied(() => PVAD.Generator.Pdf.CreateDocument(), "PVADLIC001", "facade-construction-denied");
    matrix.Run("missing");

    PvadLicense.LoadFromFile(args[0]);
    var document = PVAD.Generator.Pdf.CreateDocument();
    var emptyDocument = new PdfDocument();
    document.AddPage().DrawText("Licensed native PDF", 40, 800, 18);
    retained = document;
    matrix.Run("valid");
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
    matrix.Run("incorrect");
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
    Denied(() => document.Save(absentFile, saveOptions), "PVADLIC012", "new-file-options-export-denied");
    Check(!File.Exists(absentFile), "denied-options-export-does-not-create-file");
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
    Console.WriteLine(JsonSerializer.Serialize(new { checks, admissionMatrix = matrix.Snapshot(),
        processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
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
