using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PVAD.Core.Abstractions;
using PVAD.Core.Contracts;
using PVAD.Infrastructure.Word;
using PVAD.Licensing;

if (args.Length != 3) return 64;
var checks = new List<string>();
var design = new DesignExportDto
{
    Id = "licensed-word", Name = "Synthetic native Word",
    PageSettings = new PageSettingsDto { Width = 595, Height = 842 },
    Pages = [new PageDto { Id = "page-1", Elements =
    [new ElementDto { Id = "text-1", Type = "text", Content = "Licensed Word content", X = 30, Y = 40, Width = 300, Height = 30 }] }],
};
var before = JsonSerializer.Serialize(design);
var exporter = new WordDocumentExporter();
var options = new ExportOptions();
var matrix = new NativeAdmissionMatrix();
byte[] matrixCertificate = [];
using var matrixInput = new MemoryStream();
matrixInput.Write([1, 2, 3]);
matrixInput.Position = 1;
string Invariant() => JsonSerializer.Serialize(new { design = JsonSerializer.Serialize(design),
    input = Convert.ToHexString(matrixInput.ToArray()), matrixInput.Position, matrixInput.CanRead,
    certificate = Convert.ToHexString(matrixCertificate) });
void ExportEntry(Type type, Type[] parameters, string dispatch, Func<byte[]> export) =>
    matrix.Add(type.GetMethod("Export", parameters)!, dispatch,
        () => Docx(export(), "matrix-valid:" + dispatch + ":" + parameters.Length), Invariant);
ExportEntry(typeof(WordDocumentExporter), [typeof(DesignExportDto)], "direct", () => exporter.Export(design));
ExportEntry(typeof(WordDocumentExporter), [typeof(DesignExportDto), typeof(ExportOptions)], "direct", () => exporter.Export(design, options));
ExportEntry(typeof(IDocumentExporter), [typeof(DesignExportDto)], "interface", () => ((IDocumentExporter)exporter).Export(design));
ExportEntry(typeof(IDocumentExporter), [typeof(DesignExportDto), typeof(ExportOptions)], "interface", () => ((IDocumentExporter)exporter).Export(design, options));
ExportEntry(typeof(PVAD.Generator.Word), [typeof(DesignExportDto), typeof(ExportOptions)], "facade", () => PVAD.Generator.Word.Export(design, options));
matrix.Add(typeof(DigitalSigningService).GetMethod("SignDocx")!, "direct", () =>
{
    var signed = DigitalSigningService.SignDocx(matrixInput, matrixCertificate);
    Docx(signed, "matrix-valid:signing");
    using var zip = new ZipArchive(new MemoryStream(signed), ZipArchiveMode.Read);
    Check(zip.GetEntry("_xmlsignatures/sig1.xml") is not null, "matrix-signing-creates-opc-signature");
}, Invariant);
matrix.RequireSurface(typeof(WordDocumentExporter));
matrix.RequireSurface(typeof(IDocumentExporter));
matrix.RequireSurface(typeof(PVAD.Generator.Word));
matrix.RequireSurface(typeof(DigitalSigningService));
Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "initially-unlicensed");
Check(exporter.FormatKey == "word" && exporter.FileExtension == ".docx" && exporter.Capabilities.SupportsImages,
    "exporter-metadata-remains-free");
Check(new WordRendererCapabilities().SupportsBookmarks, "renderer-metadata-remains-free");
Denied(() => exporter.Export(design), "PVADLIC001", "direct-export-unlicensed");
Denied(() => exporter.Export(design, options), "PVADLIC001", "options-export-unlicensed");
Denied(() => PVAD.Generator.Word.Export(design), "PVADLIC001", "facade-export-unlicensed");
using var source = new MemoryStream(new byte[] { 1, 2, 3 });
Denied(() => DigitalSigningService.SignDocx(source, [1]), "PVADLIC001", "signing-unlicensed-before-certificate-read");
Check(source.Position == 0 && source.CanRead, "unlicensed-signing-preserves-stream");
matrix.Run("missing");
Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "expired-license-rejected");
Denied(() => exporter.Export(design), "PVADLIC001", "expired-load-does-not-activate-word");

PvadLicense.LoadFromFile(args[1]);
Denied(() => exporter.Export(design), "PVADLIC012", "pdf-grant-does-not-enable-word");
Denied(() => exporter.Export(design, options), "PVADLIC012", "pdf-grant-does-not-enable-options-export");
Denied(() => PVAD.Generator.Word.Export(design), "PVADLIC012", "facade-rejects-wrong-product");
Denied(() => DigitalSigningService.SignDocx(source, [1]), "PVADLIC012", "signing-rejects-wrong-product");
Check(source.Position == 0 && source.CanRead, "wrong-product-signing-preserves-stream");
Check(JsonSerializer.Serialize(design) == before, "denied-export-preserves-design");
matrix.Run("incorrect");

PvadLicense.LoadFromFile(args[0]);
Denied(() => PvadLicense.Require(PvadLicenseProducts.Pdf), "PVADLIC012", "word-license-has-no-pdf-grant");
Denied(() => PvadLicense.Require(PvadLicenseProducts.Converter), "PVADLIC012", "word-license-has-no-converter-grant");
Docx(exporter.Export(design), "word-only-direct-export");
Docx(exporter.Export(design, options), "word-only-options-export");
Docx(((IDocumentExporter)exporter).Export(design), "interface-export-has-same-boundary");
var bytes = PVAD.Generator.Word.Export(design);
Docx(bytes, "word-only-facade-export");

using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=Synthetic PVAD Word signing test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
var pfx = certificate.Export(X509ContentType.Pfx);
try
{
    matrixInput.SetLength(0);
    matrixInput.Write(bytes);
    matrixInput.Position = 0;
    matrixCertificate = pfx;
    matrix.Run("valid");
    using var signInput = new MemoryStream(bytes);
    var signed = DigitalSigningService.SignDocx(signInput, pfx);
    Docx(signed, "word-only-signing-preserves-content");
    using var zip = new ZipArchive(new MemoryStream(signed), ZipArchiveMode.Read);
    Check(zip.GetEntry("_xmlsignatures/sig1.xml") is not null, "word-only-signing-creates-opc-signature");
    Check(signInput.CanRead, "licensed-signing-leaves-source-open");
}
finally { CryptographicOperations.ZeroMemory(pfx); }

using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
try { exporter.Export(design, new ExportOptions(CancellationToken: cancellation.Token)); }
catch (OperationCanceledException) { checks.Add("licensed-export-retains-cancellation"); }
Check(checks.Contains("licensed-export-retains-cancellation"), "cancellation-not-bypassed");

PvadLicense.LoadFromFile(args[1]);
Denied(() => exporter.Export(design), "PVADLIC012", "reused-exporter-rechecks-license");
using var deniedInput = new MemoryStream(bytes);
Denied(() => DigitalSigningService.SignDocx(deniedInput, []), "PVADLIC012", "signing-rechecks-before-reading-source");
Check(deniedInput.Position == 0 && deniedInput.CanRead && deniedInput.ToArray().SequenceEqual(bytes),
    "denied-signing-preserves-source-bytes-and-position");
Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "failed-renewal-rejects-expiry");
Denied(() => exporter.Export(design), "PVADLIC012", "failed-renewal-keeps-previous-snapshot");
PvadLicense.LoadFromFile(args[0]);
Docx(exporter.Export(design), "renewal-restores-word-without-restart");
Check(JsonSerializer.Serialize(design) == before, "licensed-export-preserves-design");
Console.WriteLine(JsonSerializer.Serialize(new
{
    checks, admissionMatrix = matrix.Snapshot(),
    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
}));
return 0;

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

void Docx(byte[] content, string name)
{
    using var document = WordprocessingDocument.Open(new MemoryStream(content), false);
    Check(document.MainDocumentPart?.Document?.Descendants<Text>().Any(text => text.Text == "Licensed Word content") == true, name);
}
