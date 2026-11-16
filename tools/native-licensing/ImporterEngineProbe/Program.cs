using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.FileImporter;
using PVAD.Importer;
using PVAD.Importer.Objects;
using PVAD.Importer.Saving;
using PVAD.Importer.Trust;
using PVAD.Licensing;
using ImportPdf = PVAD.Importer.Pdf;

if (args.Length != 3) return 64;
var checks = new List<string>();
var fixtures = ImportFixtures.Create();
var pdf = fixtures["pdf"];
var inputHash = SHA256.HashData(pdf);
var readers = FileImporterRegistry.Keys.ToDictionary(key => key, FileImporterRegistry.Create);
var calls = new Dictionary<string, Func<Task>>();
var denied = true;
foreach (var (key, reader) in readers)
    calls.Add("registry:" + key, () => Read(fixtures[key], async stream => VerifyDesign(await reader.ImportAsync(stream, "synthetic." + key), key)));
calls.Add("static:doc", () => Read(fixtures["doc"], stream => Task.FromResult(DocFileImporter.Import(stream))));
calls.Add("static:docx", () => Read(fixtures["docx"], stream => Task.FromResult(DocxFileImporter.Import(stream))));
calls.Add("static:image", () => Read(fixtures["image"], stream => Task.FromResult(ImageFileImporter.Import(stream, "synthetic.png"))));
calls.Add("static:md", () => Read(fixtures["md"], stream => Task.FromResult(MarkdownFileImporter.Import(stream))));
calls.Add("static:odt", () => Read(fixtures["odt"], stream => Task.FromResult(OdtFileImporter.Import(stream))));
calls.Add("static:svg", () => Read(fixtures["svg"], stream => Task.FromResult(SvgFileImporter.Import(stream))));
calls.Add("static:pptx", () => Read(fixtures["pptx"], stream => Task.FromResult(PptxFileImporter.Import(stream))));
calls.Add("static:pdf", () => Read(pdf, stream => PdfFileImporter.DoImportAsync(stream)));
calls.Add("static:pdf-options", () => Read(pdf, stream => PdfFileImporter.DoImportAsync(stream, "source", new PdfFileImportOptions())));
calls.Add("pdf:cancellation-overload", () => Read(pdf, stream => new PdfFileImporter().ImportAsync(stream, "source", CancellationToken.None)));
calls.Add("markdown:cancellation-overload", () => Read(fixtures["md"], stream => new MarkdownFileImporter().ImportAsync(stream, "source", CancellationToken.None)));
var resolver = new CountingResolver();
calls.Add("markdown:asset-overload", () => Read(fixtures["md"], stream => new MarkdownFileImporter(resolver).ImportAsync(stream, "source", new Uri("https://example.com/"), CancellationToken.None)));
using var rsa = RSA.Create(2048);
using var certificate = new CertificateRequest("CN=Synthetic PVAD importer test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
var importer = new PdfImporter();
calls.Add("pdf:direct", () => Read(pdf, async stream => VerifyPdf(await importer.LoadAsync(stream))));
calls.Add("pdf:direct-password-overload", () => Read(pdf, async stream => VerifyPdf(await importer.LoadAsync(stream, "synthetic"))));
calls.Add("pdf:direct-certificate-overload", () => Read(pdf, async stream => VerifyPdf(await importer.LoadAsync(stream, certificate))));
calls.Add("pdf:facade", () => Read(pdf, async stream => VerifyPdf(await ImportPdf.LoadAsync(stream))));
calls.Add("pdf:facade-password-overload", () => Read(pdf, async stream => VerifyPdf(await ImportPdf.LoadEncryptedAsync(stream, "synthetic"))));
calls.Add("pdf:facade-certificate-overload", () => Read(pdf, async stream => VerifyPdf(await ImportPdf.LoadEncryptedAsync(stream, certificate))));
var rewrite = new PdfFullRewriteWriter();
var append = new PdfIncrementalUpdateWriter();
var saver = new PdfDocumentSaver();
var enumerated = false;
calls.Add("save:full-rewrite", () => VerifyBytes(rewrite.Rewrite(pdf).Bytes));
calls.Add("save:rewrite-updates", () => VerifyBytes(rewrite.RewriteWithUpdates(pdf, Updates()).Bytes));
calls.Add("save:append", () => VerifyBytes(append.Append(pdf, Updates()).Bytes));
calls.Add("save:strategy-rewrite", () => VerifyBytes(saver.Save(pdf, new PdfSaveRequest { Strategy = PdfSaveStrategy.FullRewrite }).Bytes));
calls.Add("save:strategy-incremental", () => VerifyBytes(saver.Save(pdf, new PdfSaveRequest
{
    Strategy = PdfSaveStrategy.IncrementalUpdate,
    Updates = [new(new PdfObjectId(6, 0), new PdfInteger(42))],
}).Bytes));

Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "initially-unlicensed");
Check(readers.Count == 8 && fixtures.Keys.Order().SequenceEqual(readers.Keys.Order()), "registry-and-fixture-coverage");
Check(readers.Values.All(reader => reader.SupportedExtensions.Count > 0), "registry-metadata-free");
foreach (var (name, call) in calls) await Denied(call, "PVADLIC001", "missing:" + name);
Check(!enumerated && resolver.Calls == 0, "missing-license-does-not-enumerate-updates-or-resolve-assets");
await Denied(() => Task.FromResult(PvadLicense.LoadFromFile(args[2])), "PVADLIC009", "expired-license-rejected");
using var editing = new EditingChecks(pdf);
editing.CheckMissing(checks);
using var trust = new TrustSecurityChecks(pdf);
trust.CheckMissing(checks);
using var regeneration = new RegenerationChecks(pdf);
regeneration.CheckMissing(checks);
var directEntries = DirectEngineChecks.Run(args, checks, pdf);
var editingEntries = editing.CheckLicensed(args, checks);
var trustSecurityEntries = trust.CheckLicensed(args, checks);
var regenerationEntries = regeneration.CheckLicensed(args, checks);
PvadLicense.LoadFromFile(args[0]);
denied = false;
foreach (var product in new[] { PvadLicenseProducts.Pdf, PvadLicenseProducts.Word, PvadLicenseProducts.Spreadsheet, PvadLicenseProducts.Converter })
    await Denied(() => Task.FromResult(PvadLicense.Require(product)), "PVADLIC012", "importer-only-without:" + product);
await Denied(() => Task.FromResult(new PVAD.Pdf.PdfDocument()), "PVADLIC012", "importer-license-does-not-unlock-general-pdf-generation");
foreach (var (name, call) in calls) { await call(); checks.Add("valid:" + name); }
Check(enumerated, "licensed-update-enumeration");
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    using var stream = new MemoryStream(pdf);
    try { await importer.LoadAsync(stream, cancellation.Token); }
    catch (OperationCanceledException) { checks.Add("licensed-cancellation-preserved"); }
    Check(checks.Contains("licensed-cancellation-preserved"), "cancellation-not-bypassed");
}

PvadLicense.LoadFromFile(args[1]);
denied = true;
enumerated = false;
var beforeResolver = resolver.Calls;
foreach (var (name, call) in calls) await Denied(call, "PVADLIC012", "wrong:" + name);
Check(!enumerated && resolver.Calls == beforeResolver, "wrong-license-does-not-enumerate-updates-or-resolve-assets");
Check(SHA256.HashData(pdf).SequenceEqual(inputHash), "source-pdf-unchanged");
await Denied(() => Task.FromResult(PvadLicense.LoadFromFile(args[2])), "PVADLIC009", "expired-renewal-rejected");
await Denied(calls["pdf:direct"], "PVADLIC012", "failed-renewal-preserves-snapshot");
PvadLicense.LoadFromFile(args[0]);
denied = false;
await calls["pdf:direct"]();
await calls["save:full-rewrite"]();
checks.Add("renewal-restores-existing-importer-and-writer");
var pdfContinuationChecks = await PdfContinuationChecks.Run(args, checks, pdf);
var designerPdfContinuationChecks = await DesignerPdfContinuationChecks.Run(args, checks, pdf, fixtures["image"]);
var editingContinuationChecks = await EditingContinuationChecks.Run(args, checks);
Console.WriteLine(JsonSerializer.Serialize(new { checks, coveredEntries = calls.Count, directEngineEntries = directEntries, editingEntries, trustSecurityEntries, regenerationEntries, pdfContinuationChecks, designerPdfContinuationChecks, editingContinuationChecks, scope = "file-import-pdf-load-save-direct-engines-editing-trust-and-regeneration", completeImporterEnforcement = false,
    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
return 0;

IEnumerable<PdfIncrementalObjectUpdate> Updates()
{
    enumerated = true;
    yield return new(new PdfObjectId(6, 0), new PdfInteger(42));
}
async Task Read(byte[] bytes, Func<Stream, Task> call)
{
    using var stream = new MemoryStream(bytes);
    try { await call(stream); }
    finally
    {
        if (denied && (!stream.CanRead || stream.Position != 0 || !stream.ToArray().SequenceEqual(bytes)))
            throw new InvalidOperationException("A denied import consumed, closed, or changed its source.");
    }
}
async Task VerifyBytes(byte[] bytes)
{
    using var stream = new MemoryStream(bytes);
    VerifyPdf(await importer.LoadAsync(stream));
}
void VerifyPdf(PVAD.Importer.Document.PdfDocumentModel document)
{
    if (document.Pages.Count != 1 || !document.Pages[0].TextObjects.Any(text => text.Text.Contains("Imported content", StringComparison.Ordinal)))
        throw new InvalidOperationException("Imported PDF lost its page or text.");
}
void VerifyDesign(DesignExportDto design, string format)
{
    if (design.Pages.Count == 0 || design.Pages[0].Elements.Count == 0)
        throw new InvalidOperationException($"No content imported for {format}.");
}
void Check(bool value, string name)
{
    if (!value) throw new InvalidOperationException($"Probe failed: {name}");
    checks.Add(name);
}
async Task Denied(Func<Task> call, string code, string name)
{
    try { await call(); }
    catch (PvadLicenseException error) when (error.Code == code) { checks.Add(name); return; }
    throw new InvalidOperationException($"Probe expected {code}: {name}");
}

sealed class CountingResolver : IRemoteImageResolver
{
    public int Calls { get; private set; }
    public Task<string?> ResolveAsDataUrlAsync(string source, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<string?>(null);
    }
}
