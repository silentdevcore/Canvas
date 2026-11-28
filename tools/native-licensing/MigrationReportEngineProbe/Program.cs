using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.Licensing;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Report;
using PVAD.Testing;

if (args.Length != 3) return 64;
var checks = new List<string>();
var inventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(FindRoot() + "/product-metadata/native-report-migration-entries.v1.json"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
var catalog = MigrationProviderCatalog.All.Where(p => p.Domain == "report" && p.MigrationKind == "designer" && p.ProviderKey != "auto").ToArray();
Check(catalog.Length == 8 && inventory.Entries.Length == 8, "all-eight-concrete-report-providers");
var calls = new List<Call>();
foreach (var entry in inventory.Entries)
{
    Check(catalog.Any(p => p.ProviderKey == entry.Provider), entry.Provider + ":catalog");
    var assembly = Path.GetFileName(Path.GetDirectoryName(entry.Source))!;
    var type = Assembly.Load(assembly).GetType(assembly + "." + entry.Type, true)!;
    var instance = Activator.CreateInstance(type)!;
    var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Where(m => m.Name.StartsWith("Convert")).ToArray();
    Check(type.IsSealed && methods.Length == entry.PublicConverterOverloads, entry.Provider + ":all-public-overloads");
    foreach (var method in methods)
    {
        var key = entry.Provider + ":direct:" + method.Name + ":" + method.GetParameters().Length;
        Denied(() => Invoke(method, instance, new object?[method.GetParameters().Length]), "PVADLIC001", key + ":admission-before-input-validation");
        calls.Add(new(entry.Provider, key, () =>
        {
            var source = entry.Provider == "devexpress-report" && method.Name == "Convert" ? NativeReportMigrationFixtures.DevExpressCSharp : NativeReportMigrationFixtures.Sources[entry.Provider];
            var result = Invoke(method, instance, method.GetParameters().Length == 1 ? [source] : [source, null]);
            return NativeReportMigrationFixtures.Comparable((DesignExportDto)result.GetType().GetProperty("Design")!.GetValue(result)!, result.GetType().GetProperty("Diagnostics")!.GetValue(result)!);
        }));
    }
    foreach (var factory in new[] { ReportMigrationProviders.Create(entry.Provider), ReportMigrationProviders.Create(entry.Provider.ToUpperInvariant()) })
    {
        var local = factory;
        calls.Add(new(entry.Provider, entry.Provider + ":factory:" + (calls.Count), () =>
        {
            var result = local.Convert(NativeReportMigrationFixtures.Sources[entry.Provider]);
            return NativeReportMigrationFixtures.Comparable(result.Design, result.Diagnostics);
        }));
    }
}
calls.Add(new("devexpress-report", "resource:direct", () => JsonSerializer.Serialize(PVAD.Migration.Report.Designer.DevExpress.DevExpressReportResourceParser.ParseResx(NativeReportMigrationFixtures.ResourceXml))));
calls.Add(new("devexpress-report", "resource:compatibility", () => JsonSerializer.Serialize(DevExpressReportResourceParser.ParseResx(NativeReportMigrationFixtures.ResourceXml))));
Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "initially-unlicensed");
foreach (var call in calls) Denied(() => call.Run(), "PVADLIC001", "missing:" + call.Key);
foreach (var profile in new[] { "pdf", "importer", "converter", "migration-code", "migration-viewer", "migration-designer" })
{
    Load(profile);
    foreach (var call in calls) Denied(() => call.Run(), "PVADLIC012", profile + ":denied:" + call.Key);
}
Load("migration-report");
var baseline = calls.ToDictionary(call => call.Key, call => call.Run());
foreach (var call in calls) checks.Add("valid:" + call.Key);
foreach (var provider in inventory.Entries.Select(e => e.Provider))
{
    Load("migration.report.designer-" + provider);
    foreach (var call in calls)
        if (call.Provider == provider) Check(call.Run() == baseline[call.Key], provider + ":admitted:" + call.Key);
        else Denied(() => call.Run(), "PVADLIC012", provider + ":other-provider-denied:" + call.Key);
    foreach (var product in PvadLicenseProducts.All.Where(p => p != "migration.report.designer"))
        Denied(() => PvadLicense.Require(product, product.StartsWith("migration.") ? provider : null), "PVADLIC012", provider + ":no-other-product:" + product);
}
Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "expired-renewal-rejected");
Load("migration-report");
foreach (var call in calls) Check(call.Run() == baseline[call.Key], "renewed:" + call.Key);
Check(checks.Count == checks.Distinct().Count(), "unique-check-identifiers");
Console.WriteLine(JsonSerializer.Serialize(new { checks, entries = calls.Count, providers = 8,
    admissionEvidence = calls.Select(call => new
    {
        product = "migration.report.designer", provider = call.Provider, path = call.Key,
        missing = "missing:" + call.Key, incorrect = "pdf:denied:" + call.Key, valid = "valid:" + call.Key,
        outputChecksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(baseline[call.Key]))),
    }),
    outputChecksums = baseline.ToDictionary(pair => pair.Key, pair => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pair.Value)))) }));
return 0;

void Load(string profile) => PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(args[0])!, profile + ".json"));
void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
void Denied(Action call, string code, string name)
{
    try { call(); throw new InvalidOperationException(name + ": unexpectedly admitted"); }
    catch (PvadLicenseException exception) { Check(exception.Code == code, name); }
}
static object Invoke(MethodInfo method, object instance, object?[] arguments)
{
    try { return method.Invoke(instance, arguments)!; }
    catch (TargetInvocationException exception) when (exception.InnerException is not null) { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
}
static string FindRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VERSION"))) directory = directory.Parent;
    return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
}
sealed record Inventory(Entry[] Entries);
sealed record Entry(string Provider, string Source, string Type, int PublicConverterOverloads);
sealed record Call(string Provider, string Key, Func<string> Run);
