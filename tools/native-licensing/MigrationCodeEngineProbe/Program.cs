using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Licensing;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Pdf;
using PVAD.Migration.Roslyn;
using PVAD.Migration.Spreadsheet;

if (args.Length != 3) return 64;
var checks = new List<string>();
var catalog = MigrationProviderCatalog.All.Where(p => p.MigrationKind == "code").ToArray();
Check(catalog.Length == 24 && catalog.Count(p => p.Domain == "pdf") == 16 && catalog.Count(p => p.Domain == "spreadsheet") == 8, "complete-code-provider-catalog");
var inventoryPath = FindRoot() + "/product-metadata/native-code-migration-entries.v1.json";
var inventory = JsonSerializer.Deserialize<Inventory>(File.ReadAllText(inventoryPath), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
Check(inventory.Entries.Length == catalog.Length, "complete-entry-inventory");
var entries = catalog.Select(descriptor =>
{
    var product = "migration." + descriptor.Domain + ".code";
    var record = inventory.Entries.Single(entry => entry.Product == product && entry.Provider == descriptor.ProviderKey);
    var assembly = Path.GetFileName(Path.GetDirectoryName(record.Source))!;
    var type = Assembly.Load(assembly).GetType(assembly + "." + record.Type, throwOnError: true)!;
    Check(type.IsSealed && type.BaseType == typeof(CSharpSourceMigration), product + ":" + descriptor.ProviderKey + ":sealed-direct-provider");
    var direct = (ISourceMigration)Activator.CreateInstance(type)!;
    var factory = descriptor.Domain == "pdf" ? PdfMigrationProviders.Create(descriptor.ProviderKey) : SpreadsheetMigrationProviders.Create(descriptor.ProviderKey);
    return new Entry(descriptor, product, direct, factory);
}).ToArray();
const string source = "var migrationValue = 42;";
MigrationAnalysisChecks.Unlicensed(checks);
foreach (var entry in entries)
{
    Check(MigrationProviderCatalog.Get(entry.Descriptor.Domain, "code", entry.Descriptor.ProviderKey) == entry.Descriptor, entry.Id + ":metadata-free");
    foreach (var call in Calls(entry))
    {
        Denied(() => call.Run(source), "PVADLIC001", entry.Id + ":" + call.Name + ":unlicensed");
        if (call.Name != "pdf-aggregate") Denied(() => call.Run(null!), "PVADLIC001", entry.Id + ":" + call.Name + ":admission-before-input");
    }
}
Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "expired-license-rejected");
Check(PvadLicense.GetStatus().State == PvadLicenseState.Unlicensed, "expired-license-not-activated");
PvadLicense.LoadFromFile(args[1]);
foreach (var entry in entries)
    foreach (var call in Calls(entry)) Denied(() => call.Run(source), "PVADLIC012", entry.Id + ":" + call.Name + ":pdf-not-migration-right");

PvadLicense.LoadFromFile(args[0]);
foreach (var product in new[] { "pdf", "word", "spreadsheet", "importer", "converter" })
    Denied(() => PvadLicense.Require(product), "PVADLIC012", "migration-license-not-generator:" + product);
var baselines = entries.ToDictionary(entry => entry.Id, entry => entry.Direct.Migrate(source).MigratedCode);
foreach (var entry in entries)
{
    foreach (var call in Calls(entry)) Equivalent(call.Run(source), baselines[entry.Id], entry.Id + ":" + call.Name + ":licensed");
    foreach (var alias in entry.Descriptor.Aliases)
    {
        var canonical = MigrationProviderCatalog.Get(entry.Descriptor.Domain, "code", alias);
        Check(canonical.ProviderKey == entry.Descriptor.ProviderKey, entry.Id + ":canonical-alias:" + alias);
        var migration = canonical.Domain == "pdf" ? PdfMigrationProviders.Create(canonical.ProviderKey) : SpreadsheetMigrationProviders.Create(canonical.ProviderKey);
        Equivalent(migration.Migrate(source), baselines[entry.Id], entry.Id + ":alias-output:" + alias);
    }
}

// Every single-provider license must reject all other domains and technologies,
// including the same vendor family in the other document domain.
foreach (var licensed in entries)
{
    PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(args[0])!, licensed.Product + "-" + licensed.Descriptor.ProviderKey + ".json"));
    foreach (var target in entries)
    {
        if (target.Id == licensed.Id)
        {
            foreach (var call in Calls(target)) Equivalent(call.Run(source), baselines[target.Id], licensed.Id + ":only:" + call.Name);
        }
        else Denied(() => target.Direct.Migrate(source), "PVADLIC012", licensed.Id + ":rejects:" + target.Id);
    }
    Denied(() => PvadLicense.Require(licensed.Product, licensed.Descriptor.Aliases[0]), "PVADLIC012", licensed.Id + ":alias-not-license-key");
}

PvadLicense.LoadFromFile(args[1]);
Denied(() => PvadLicense.LoadFromFile(args[2]), "PVADLIC009", "failed-renewal-rejected");
foreach (var entry in entries)
    Denied(() => entry.Direct.Migrate(source), "PVADLIC012", entry.Id + ":failed-renewal-preserves-snapshot");
PvadLicense.LoadFromFile(args[0]);
foreach (var entry in entries)
{
    Equivalent(entry.Direct.Migrate(source), baselines[entry.Id], entry.Id + ":renewal-restores-provider");
    try { entry.Direct.Migrate(""); throw new InvalidOperationException("Expected original source validation."); }
    catch (ArgumentException) { Check(true, entry.Id + ":valid-rights-preserve-input-errors"); }
}
var analysisChecks = MigrationAnalysisChecks.Run(args, checks);
Check(checks.Distinct().Count() == checks.Count, "unique-check-identifiers");
Console.WriteLine(JsonSerializer.Serialize(new { checks, providers = entries.Length, pdfProviders = 16, spreadsheetProviders = 8,
    analysisChecks,
    outputChecksums = baselines.ToDictionary(pair => pair.Key, pair => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pair.Value)))),
    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
return 0;

IEnumerable<(string Name, Func<string, MigrationResult> Run)> Calls(Entry entry)
{
    yield return ("direct", entry.Direct.Migrate);
    yield return ("abstract-base", ((CSharpSourceMigration)entry.Direct).Migrate);
    yield return ("factory-interface", entry.Factory.Migrate);
    if (entry.Descriptor.Domain == "pdf") yield return ("pdf-aggregate", text => PdfMigrationProviders.Migrate(entry.Descriptor.ProviderKey, text));
    else yield return ("spreadsheet-aggregate", text => SpreadsheetMigrationProviders.Migrate(entry.Descriptor.ProviderKey, text));
}
void Equivalent(MigrationResult result, string expected, string name)
{ Check(result.MigratedCode == expected && result.MigratedCode.Contains("42", StringComparison.Ordinal), name); }
void Check(bool condition, string name)
{ if (!condition) throw new InvalidOperationException("Migration probe failed: " + name); checks.Add(name); }
void Denied(Action action, string code, string name)
{
    try { action(); } catch (PvadLicenseException error) when (error.Code == code) { Check(true, name); return; }
    throw new InvalidOperationException("Expected migration admission denial: " + name);
}
static string FindRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VERSION"))) directory = directory.Parent;
    return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
}
sealed record Inventory(InventoryEntry[] Entries);
sealed record InventoryEntry(string Product, string Provider, string Source, string Type);
sealed record Entry(MigrationProviderDescriptor Descriptor, string Product, ISourceMigration Direct, ISourceMigration Factory)
{ public string Id => Product + ":" + Descriptor.ProviderKey; }
