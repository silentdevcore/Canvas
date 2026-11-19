using System.Security.Cryptography;
using System.Text.Json;
using PVAD.Migration.Abstractions;

namespace PVAD.Licensing.Tests;

public sealed class NativeLicenseContractTests
{
    public static IEnumerable<object[]> Providers => MigrationProviderCatalog.All.Select(provider =>
        new object[] { $"migration.{provider.Domain}.{provider.MigrationKind}", provider.ProviderKey });

    [Theory]
    [MemberData(nameof(Providers))]
    public void EveryRegisteredProviderHasAnExplicitScopedNativeGrant(string product, string provider)
    {
        Assert.Contains(product, PvadLicenseProducts.All);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow;
        var payload = new PvadLicensePayload(1, Guid.NewGuid(), "synthetic-provider-test", PvadLicenseModel.Trial,
            now.AddDays(-1), now.AddDays(-1), now.AddDays(29), null, false, [new(product, [provider])]);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PvadLicenseJson.Options);
        var envelope = new PvadSignedNativeLicense(1, "test", PvadLicenseJson.Algorithm, Convert.ToBase64String(bytes),
            Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        var verifier = new PvadLicenseVerifier(new Dictionary<string, string> { ["test"] = key.ExportSubjectPublicKeyInfoPem() });
        var authority = new PvadLicenseAuthority(verifier, TimeProvider.System, now.AddYears(-1));
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(envelope, PvadLicenseJson.Options));
        if (provider == "auto")
        {
            // Detection is routing, not a licensed technology. Check the detected concrete provider instead.
            Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => authority.Load(stream)).Code);
            return;
        }
        authority.Load(stream);
        Assert.Equal(provider, authority.Require(product, provider).Provider);
        Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Require(product, "not-licensed-provider")).Code);
        foreach (var other in PvadLicenseProducts.All.Where(PvadLicenseProducts.IsMigration).Where(other => other != product))
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Require(other, provider)).Code);
    }

    [Fact]
    public void ProductMatrixMatchesTheActualNativeContract()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root(), "product-metadata/native-library-products.v1.json")));
        var products = document.RootElement.GetProperty("products").EnumerateArray().ToArray();
        Assert.Equal(PvadLicenseProducts.All.Order(), products.Select(value => value.GetProperty("id").GetString()!).Order());
        Assert.Equal("all-native-entry-points", document.RootElement.GetProperty("enforcementStatus").GetString());
        Assert.All(products, product => Assert.True(product.GetProperty("enforced").GetBoolean()));
        var importer = products.Single(product => product.GetProperty("id").GetString() == "importer");
        Assert.Empty(importer.GetProperty("remainingEntryPoints").EnumerateArray());
        Assert.StartsWith("partial-", importer.GetProperty("acceptanceStatus").GetString());
        Assert.NotEmpty(importer.GetProperty("remainingAcceptance").EnumerateArray());
    }

    [Fact]
    public void CodeProviderEntryInventoryMatchesCatalogAndDirectGuards()
    {
        using var inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root(), "product-metadata/native-code-migration-entries.v1.json")));
        Assert.Equal("provider-entry-points-only", inventory.RootElement.GetProperty("scope").GetString());
        var entries = inventory.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        var providers = MigrationProviderCatalog.All.Where(provider => provider.MigrationKind == "code").ToArray();
        Assert.Equal(24, entries.Length);
        Assert.Equal(providers.Length, entries.Length);
        Assert.Equal(entries.Length, entries.Select(entry => entry.GetProperty("product").GetString() + ":" + entry.GetProperty("provider").GetString()).Distinct().Count());
        foreach (var provider in providers)
        {
            var product = "migration." + provider.Domain + ".code";
            var entry = Assert.Single(entries, entry => entry.GetProperty("product").GetString() == product && entry.GetProperty("provider").GetString() == provider.ProviderKey);
            var source = File.ReadAllText(Path.Combine(Root(), entry.GetProperty("source").GetString()!));
            var constant = provider.Domain == "pdf" ? "PdfCodeMigration" : "SpreadsheetCodeMigration";
            Assert.Contains($"CodeMigrationAdmission.Begin(PvadLicenseProducts.{constant}, \"{provider.ProviderKey}\")", source);
            Assert.Contains($"CodeMigrationAdmission.Demand(PvadLicenseProducts.{constant}, \"{provider.ProviderKey}\");", source);
        }
    }

    [Fact]
    public void NativeReleaseMetadataMatchesVersionAndPublishedDate()
    {
        var root = Root();
        var version = File.ReadAllText(Path.Combine(root, "VERSION")).Trim();
        using var metadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "src/Licensing/PVAD.Licensing/LibraryRelease.json")));
        using var releases = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "product-metadata/pvad-releases.json")));
        var current = releases.RootElement.GetProperty("releases").EnumerateArray().Single(value => value.GetProperty("version").GetString() == version);
        Assert.Equal(version, metadata.RootElement.GetProperty("version").GetString());
        Assert.Equal(current.GetProperty("publishedAt").GetString(), metadata.RootElement.GetProperty("publishedAt").GetString());
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VERSION")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
