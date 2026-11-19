using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Licensing;
using PVAD.Migration.Pdf.Viewer.Nutrient;

if (args.Length != 3) return 64;
var checks = new List<string>();
const string source = "NutrientViewer.load({ document: '/synthetic.pdf' });";
var pages = NutrientViewerExamples.ComprehensivePageGeometry;
var json = NutrientViewerExamples.ComprehensiveInstantJson;
var sourceConverter = new NutrientViewerSourceConverter();
var jsonConverter = new NutrientInstantJsonConverter();
var calls = new Dictionary<string, Func<string>>
{
    ["javascript"] = () => JsonSerializer.Serialize(sourceConverter.Convert(source)),
    ["typescript"] = () => JsonSerializer.Serialize(sourceConverter.ConvertTranspiledTypeScript(source, source)),
    ["instant-json"] = () => ComparableJson(jsonConverter.Convert(json, pages)),
};
Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "initially-unlicensed");
Check(pages.Count > 0 && json.Length > 0, "examples-free-without-license");
foreach (var (name, call) in calls) Denied(() => call(), "PVADLIC001", "missing:" + name);
Denied(() => sourceConverter.Convert(null!), "PVADLIC001", "admission-before-source-validation");
Denied(() => jsonConverter.Convert(null!, null!), "PVADLIC001", "admission-before-json-validation");
Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "expired-license-rejected");
foreach (var profile in new[] { "pdf", "word", "spreadsheet", "importer", "converter", "migration-code", "migration-designer" })
{
    Load(profile);
    foreach (var (name, call) in calls) Denied(() => call(), "PVADLIC012", profile + ":denied:" + name);
}
PvadLicense.LoadFromFile(args[0]);
var baselines = calls.ToDictionary(pair => pair.Key, pair => pair.Value());
foreach (var product in new[] { "pdf", "word", "spreadsheet", "importer", "converter", "migration.pdf.designer", "migration.pdf.code" })
    Denied(() => PvadLicense.Require(product, product.StartsWith("migration.") ? "nutrient-viewer" : null), "PVADLIC012", "viewer-does-not-grant:" + product);
foreach (var provider in new[] { "nutrient", "Nutrient", "other-provider" })
    Denied(() => PvadLicense.Require("migration.pdf.viewer", provider), "PVADLIC012", "canonical-provider-only:" + provider);
foreach (var kind in new[] { "typescript", "instant-json" })
{
    PvadLicense.LoadFromFile(args[0]);
    var fired = 0;
    void Replace()
    {
        fired++;
        Load("pdf");
        foreach (var (name, call) in calls) Denied(() => call(), "PVADLIC012", kind + ":reentrant:" + name);
    }
    if (kind == "typescript")
    {
        var result = sourceConverter.ConvertTranspiledTypeScript(source, source, compilerDiagnostics: new CallbackList<string>([], Replace));
        Check(JsonSerializer.Serialize(result) == baselines[kind], kind + ":admitted-output-unchanged");
    }
    else
    {
        var result = jsonConverter.Convert(json, new CallbackList<NutrientPageGeometry>(pages, Replace));
        Check(ComparableJson(result) == baselines[kind], kind + ":admitted-output-unchanged");
    }
    Check(fired > 0, kind + ":caller-callback-exercised");
    foreach (var (name, call) in calls) Denied(() => call(), "PVADLIC012", kind + ":subsequent-denied:" + name);
}
PvadLicense.LoadFromFile(args[0]);
var originalError = new IOException("Synthetic compiler callback failure.");
try
{
    sourceConverter.ConvertTranspiledTypeScript(source, source, compilerDiagnostics: new CallbackList<string>([], () => throw originalError));
    throw new InvalidOperationException("Expected caller exception.");
}
catch (IOException exception) { Check(ReferenceEquals(exception, originalError), "caller-error-identity-preserved"); }
foreach (var (name, call) in calls) Check(call() == baselines[name], "renewed:" + name);
Check(checks.Count == checks.Distinct().Count(), "unique-check-identifiers");
Console.WriteLine(JsonSerializer.Serialize(new { checks, outputChecksums = baselines.ToDictionary(pair => pair.Key,
    pair => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pair.Value)))),
    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
return 0;

void Load(string profile) => PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(args[0])!, profile + ".json"));
void Check(bool result, string name) { if (!result) throw new InvalidOperationException(name); checks.Add(name); }
void Denied(Action call, string code, string name)
{
    try { call(); throw new InvalidOperationException(name + ": unexpectedly admitted"); }
    catch (PvadLicenseException exception) { Check(exception.Code == code, name); }
}

static string ComparableJson(NutrientInstantJsonResult result)
{
    static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    if (result.NormalizedChecksum != Hash(result.AnnotationSidecarJson) ||
        result.GeneratedChecksum != Hash(result.AnnotationSidecarJson + "\n" + result.FormValuePayloadJson))
        throw new InvalidOperationException("The actual output checksums must remain valid.");
    var sidecar = JsonSerializer.Deserialize<PvadViewerAnnotationSidecar>(result.AnnotationSidecarJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    if (sidecar.ExportedAt < DateTimeOffset.UtcNow.AddMinutes(-5) || sidecar.ExportedAt > DateTimeOffset.UtcNow.AddMinutes(1))
        throw new InvalidOperationException("Expected the original export timestamp behavior.");
    // The pre-existing export timestamp is intentionally nondeterministic.
    return JsonSerializer.Serialize(result with { AnnotationSidecarJson = JsonSerializer.Serialize(sidecar with { ExportedAt = DateTimeOffset.UnixEpoch }),
        NormalizedChecksum = "validated-per-output", GeneratedChecksum = "validated-per-output" });
}

sealed class CallbackList<T>(IReadOnlyList<T> items, Action callback) : IReadOnlyList<T>
{
    private int invoked;
    private void Invoke() { if (Interlocked.Exchange(ref invoked, 1) == 0) callback(); }
    public int Count { get { Invoke(); return items.Count; } }
    public T this[int index] { get { Invoke(); return items[index]; } }
    public IEnumerator<T> GetEnumerator() { Invoke(); return items.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
