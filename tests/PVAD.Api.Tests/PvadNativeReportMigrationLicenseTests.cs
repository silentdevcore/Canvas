using System.Collections;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using PVAD.Licensing;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Report;
using PVAD.Testing;
using PVAD.WebApi.Services;

namespace PVAD.Api.Tests;

[Collection("Native spreadsheet license renewal")]
public sealed class PvadNativeReportMigrationLicenseTests
{
    private static string Integrated => PvadTestEnvironment.Read("PVAD_NATIVE_LICENSE_PATH")!;
    private static void Load(string profile) => PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(Integrated)!, profile + ".json"));

    [Fact]
    public async Task Canonical_alias_auto_and_legacy_report_routes_keep_native_license_problems()
    {
        using var factory = new PvadApiTestFactory();
        using var client = factory.CreateClient();
        try
        {
            foreach (var profile in new[] { "pdf", "migration-code", "migration-viewer", "migration-designer", "migration-report" })
            {
                Load(profile);
                foreach (var (provider, source) in NativeReportMigrationFixtures.Sources)
                {
                    var alias = MigrationProviderCatalog.Get("report", "designer", provider).Aliases.First();
                    foreach (var key in new[] { provider, alias, "auto" })
                    {
                        using var response = await client.PostAsJsonAsync($"/api/pvad/v1/migrations/report/designer/{key}/convert", new { source });
                        if (profile != "migration-report") { await Denied(response); continue; }
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        Assert.Equal(provider, result.RootElement.GetProperty("provider").GetString());
                        Assert.Equal("design", result.RootElement.GetProperty("artifacts")[0].GetProperty("id").GetString());
                    }
                    using var legacy = await client.PostAsJsonAsync("/api/pvad/migration/report-to-design", new { sourceCode = source });
                    if (profile == "migration-report") Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
                    else await Denied(legacy);
                }
            }
        }
        finally { PvadLicense.LoadFromFile(Integrated); }
    }

    [Fact]
    public void All_provider_composites_finish_under_original_admission_after_resource_callback_replacement()
    {
        var service = new ReportMigrationService();
        try
        {
            foreach (var (provider, source) in NativeReportMigrationFixtures.Sources)
            {
                Load("migration-report");
                var baseline = service.Convert(new(provider, source, new Dictionary<string, string> { ["synthetic"] = "synthetic resource" }, NativeReportMigrationFixtures.ResourceXml));
                var resources = new CallbackResources(new Dictionary<string, string> { ["synthetic"] = "synthetic resource" }, () =>
                {
                    Load("pdf");
                    Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => ReportMigrationProviders.Create(provider).Convert(source)).Code);
                    Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => service.Convert(new(provider, source))).Code);
                });
                var converted = service.Convert(new(provider, source, resources, NativeReportMigrationFixtures.ResourceXml));
                Assert.Equal(1, resources.Calls);
                Assert.Equal(NativeReportMigrationFixtures.Comparable(baseline.Design, baseline.Diagnostics), NativeReportMigrationFixtures.Comparable(converted.Design, converted.Diagnostics));
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => service.Convert(new("auto", source))).Code);
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => DevExpressReportResourceParser.ParseResx(NativeReportMigrationFixtures.ResourceXml)).Code);
                Load("migration-report");
                Assert.Equal(provider, service.Convert(new("auto", source)).Provider);
            }
            var originalError = new IOException("Synthetic resource callback error.");
            Assert.Same(originalError, Assert.Throws<IOException>(() => service.Convert(new("rdl", NativeReportMigrationFixtures.Sources["rdl"],
                new CallbackResources(new Dictionary<string, string>(), () => throw originalError)))));
            Assert.Equal("rdl", service.Convert(new("rdl", NativeReportMigrationFixtures.Sources["rdl"])).Provider);
            foreach (var product in new[] { "pdf", "importer", "converter", "word", "spreadsheet" })
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => PvadLicense.Require(product)).Code);
        }
        finally { PvadLicense.LoadFromFile(Integrated); }
    }

    [Fact]
    public void Auto_detection_and_binary_packages_require_concrete_provider_before_resources_are_read()
    {
        var service = new ReportMigrationService();
        try
        {
            foreach (var (provider, source) in NativeReportMigrationFixtures.Sources)
            {
                Assert.Equal(provider, ReportMigrationService.DetectProvider(source));
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(source));
                var untouched = new CallbackResources(new Dictionary<string, string>(), () => throw new InvalidOperationException("Resources must not be read before admission."));
                Load("pdf");
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => service.Convert(new("auto", null, untouched, SourceBase64: encoded))).Code);
                Assert.Equal(0, untouched.Calls);
                Load("migration-report");
                Assert.Equal(provider, service.Convert(new("auto", null, ResourceXml: NativeReportMigrationFixtures.ResourceXml, SourceBase64: encoded)).Provider);
            }
            using var package = new MemoryStream();
            using (var zip = new ZipArchive(package, ZipArchiveMode.Create, true))
            {
                using var writer = new StreamWriter(zip.CreateEntry("synthetic.rdl").Open());
                writer.Write(NativeReportMigrationFixtures.Sources["rdl"]);
            }
            var encodedPackage = Convert.ToBase64String(package.ToArray());
            Load("pdf");
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => service.Convert(new("auto", null, SourceBase64: encodedPackage))).Code);
            Load("migration-report");
            Assert.Equal("rdl", service.Convert(new("auto", null, SourceBase64: encodedPackage)).Provider);
        }
        finally { PvadLicense.LoadFromFile(Integrated); }
    }

    [Fact]
    public void Direct_rpx_subreport_resolution_retains_admission_but_reentrant_public_calls_stay_fresh()
    {
        const string parent = "<Report Name=\"Parent\"><Sections><Detail Name=\"Detail\" Height=\"1\"><Controls><SubReport Name=\"sub\" Left=\"0\" Top=\"0\" Width=\"3\" Height=\"1\" ReportName=\"child.rpx\" /></Controls></Detail></Sections></Report>";
        try
        {
            Load("migration-report");
            var resources = new CallbackResources(new Dictionary<string, string> { ["child.rpx"] = NativeReportMigrationFixtures.Sources["rpx"] }, () =>
            {
                Load("pdf");
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => new RpxReportMigration().Convert(parent)).Code);
            });
            var result = new PVAD.Migration.Report.Designer.Rpx.RpxToDesignConverter().Convert(parent, resources);
            Assert.Equal(1, resources.Calls);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGRPX017");
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => new RpxReportMigration().Convert(parent)).Code);
        }
        finally { PvadLicense.LoadFromFile(Integrated); }
    }

    private static async Task Denied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Content.Headers.ContentDisposition);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PVADLIC012", json.RootElement.GetProperty("code").GetString());
    }

    private sealed class CallbackResources(IReadOnlyDictionary<string, string> values, Action callback) : IReadOnlyDictionary<string, string>
    {
        public int Calls { get; private set; }
        private void Visit() { if (Calls == 0) { Calls++; callback(); } }
        public int Count => values.Count;
        public IEnumerable<string> Keys => values.Keys;
        public IEnumerable<string> Values => values.Values;
        public string this[string key] { get { Visit(); return values[key]; } }
        public bool ContainsKey(string key) { Visit(); return values.ContainsKey(key); }
        public bool TryGetValue(string key, out string value) { Visit(); return values.TryGetValue(key, out value!); }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() { Visit(); return values.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
