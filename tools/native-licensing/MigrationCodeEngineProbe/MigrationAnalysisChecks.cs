using System.Collections;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using PVAD.Licensing;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Pdf;
using PVAD.Migration.Roslyn;

internal static class MigrationAnalysisChecks
{
    private const string Source = "document.SignPDF(); document.AddEmbeddedFile(\"invoice.xml\");";
    internal static object[] AdmissionEvidence { get; private set; } = [];

    internal static void Unlicensed(List<string> checks)
    {
        foreach (var profile in PdfTrustMigrationCatalog.All)
        {
            Denied(() => CSharpPdfTrustEvidenceScanner.Scan(Source, profile), "PVADLIC001", "unlicensed-trust:" + profile.ProviderKey, checks);
            Denied(() => PdfEInvoiceMigrationCatalog.Analyze(profile.ProviderKey, Source), "PVADLIC001", "unlicensed-invoice:" + profile.ProviderKey, checks);
        }
    }

    internal static int Run(string[] licenses, List<string> checks)
    {
        var start = checks.Count;
        var profiles = PdfTrustMigrationCatalog.All;
        PvadLicense.LoadFromFile(licenses[0]);
        var baselines = profiles.ToDictionary(profile => profile.ProviderKey, profile => (
            Trust: JsonSerializer.Serialize(CSharpPdfTrustEvidenceScanner.Scan(Source, profile)),
            Invoice: JsonSerializer.Serialize(PdfEInvoiceMigrationCatalog.Analyze(profile.ProviderKey, Source))));
        PvadLicense.LoadFromFile(licenses[1]);
        foreach (var profile in profiles)
        {
            Denied(() => CSharpPdfTrustEvidenceScanner.Scan(Source, profile), "PVADLIC012", "wrong-product-trust:" + profile.ProviderKey, checks);
            Denied(() => PdfEInvoiceMigrationCatalog.Analyze(profile.ProviderKey, Source), "PVADLIC012", "wrong-product-invoice:" + profile.ProviderKey, checks);
        }
        var providers = MigrationProviderCatalog.All.Where(provider => provider.MigrationKind == "code");
        foreach (var licensed in providers)
        {
            var product = "migration." + licensed.Domain + ".code";
            PvadLicense.LoadFromFile(Path.Combine(Path.GetDirectoryName(licenses[0])!, product + "-" + licensed.ProviderKey + ".json"));
            foreach (var profile in profiles)
            {
                var name = product + ":" + licensed.ProviderKey + ":" + profile.ProviderKey;
                if (licensed.Domain == "pdf" && licensed.ProviderKey == profile.ProviderKey)
                {
                    Check(JsonSerializer.Serialize(CSharpPdfTrustEvidenceScanner.Scan(Source, profile)) == baselines[profile.ProviderKey].Trust, name + ":trust-output");
                    Check(JsonSerializer.Serialize(PdfEInvoiceMigrationCatalog.Analyze(profile.ProviderKey, Source)) == baselines[profile.ProviderKey].Invoice, name + ":invoice-output");
                }
                else
                {
                    Denied(() => CSharpPdfTrustEvidenceScanner.Scan(Source, profile), "PVADLIC012", name + ":trust-denied", checks);
                    Denied(() => PdfEInvoiceMigrationCatalog.Analyze(profile.ProviderKey, Source), "PVADLIC012", name + ":invoice-denied", checks);
                }
            }
        }

        var original = PdfTrustMigrationCatalog.Get("aspose-pdf");
        PvadLicense.LoadFromFile(licenses[0]);
        var changes = 0;
        var callbackProfile = original with { Capabilities = new CallbackList<PdfTrustProviderCapability>(original.Capabilities, () =>
        {
            changes++;
            PvadLicense.LoadFromFile(licenses[1]);
            Denied(() => CSharpPdfTrustEvidenceScanner.Scan(Source, original), "PVADLIC012", "trust-callback-public-analysis-fresh", checks);
            Denied(() => PdfMigrationProviders.Migrate(original.ProviderKey, Source), "PVADLIC012", "trust-callback-public-conversion-fresh", checks);
            Denied(() => PdfMigrationProviders.Create("nutrient").Migrate(Source), "PVADLIC012", "trust-callback-other-provider-fresh", checks);
        }) };
        Check(JsonSerializer.Serialize(CSharpPdfTrustEvidenceScanner.Scan(Source, callbackProfile)) == baselines[original.ProviderKey].Trust && changes == 1, "trust-retains-admitted-output");
        Denied(() => PdfEInvoiceMigrationCatalog.Analyze(original.ProviderKey, Source), "PVADLIC012", "trust-next-public-analysis-denied", checks);

        foreach (var nativeFailure in new[] { false, true })
        {
            PvadLicense.LoadFromFile(licenses[0]);
            var failing = original with { Capabilities = new CallbackList<PdfTrustProviderCapability>(original.Capabilities, () =>
            {
                PvadLicense.LoadFromFile(licenses[1]);
                if (nativeFailure) PvadLicense.Require(PvadLicenseProducts.PdfCodeMigration, original.ProviderKey);
                throw new IOException("Synthetic migration analysis input failure.");
            }) };
            try { CSharpPdfTrustEvidenceScanner.Scan(Source, failing); throw new InvalidOperationException("Expected original callback failure."); }
            catch (Exception error) when (nativeFailure ? error is PvadLicenseException { Code: "PVADLIC012" } : error is IOException)
            { Check(true, "trust-original-error:" + nativeFailure); }
            Denied(() => PdfMigrationProviders.Migrate(original.ProviderKey, Source), "PVADLIC012", "trust-failed-scope-closed:" + nativeFailure, checks);
        }

        PvadLicense.LoadFromFile(licenses[0]);
        Check(JsonSerializer.Serialize(PdfEInvoiceMigrationCatalog.Analyze(original.ProviderKey, Source)) == baselines[original.ProviderKey].Invoice, "invoice-licensed-output");
        PvadLicense.LoadFromFile(licenses[1]);
        Denied(() => PdfEInvoiceMigrationCatalog.Analyze(original.ProviderKey, Source), "PVADLIC012", "invoice-next-public-analysis-denied", checks);
        PvadLicense.LoadFromFile(licenses[0]);
        Check(JsonSerializer.Serialize(PdfEInvoiceMigrationCatalog.Analyze(original.ProviderKey, Source)) == baselines[original.ProviderKey].Invoice, "invoice-renewal-output");
        AdmissionEvidence = profiles.SelectMany(profile => new[] { "trust", "invoice" }.Select(operation => (object)new
        {
            product = "migration.pdf.code", provider = profile.ProviderKey, operation,
            missing = "analysis:unlicensed-" + operation + ":" + profile.ProviderKey,
            incorrect = "analysis:wrong-product-" + operation + ":" + profile.ProviderKey,
            valid = "analysis:migration.pdf.code:" + profile.ProviderKey + ":" + profile.ProviderKey + ":" + operation + "-output",
            outputChecksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operation == "trust" ? baselines[profile.ProviderKey].Trust : baselines[profile.ProviderKey].Invoice))),
        })).ToArray();
        return checks.Count - start;

        void Check(bool valid, string name)
        { if (!valid) throw new InvalidOperationException("Migration analysis failed: " + name); checks.Add("analysis:" + name); }
    }

    private static void Denied(Action action, string code, string name, List<string> checks)
    {
        try { action(); } catch (PvadLicenseException error) when (error.Code == code) { checks.Add("analysis:" + name); return; }
        throw new InvalidOperationException("Expected migration analysis denial: " + name);
    }

    private sealed class CallbackList<T>(IReadOnlyList<T> values, Action action) : IReadOnlyList<T>
    {
        private bool called;
        public int Count => values.Count;
        public T this[int index] => values[index];
        public IEnumerator<T> GetEnumerator()
        {
            if (!called) { called = true; action(); }
            return values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
