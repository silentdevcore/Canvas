using System.Security.Cryptography;
using System.Text.Json;

namespace PVAD.Licensing.Tests;

public sealed class ScopedNativeLicenseTests
{
    private static PvadLicensePayload Payload(params string[] capabilities)
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-1);
        return new(2, Guid.NewGuid(), "synthetic-individual", PvadLicenseModel.Trial,
            start, start, start.AddDays(30), null, false,
            [new("pdf", []), new("word", []), new("spreadsheet", []), new("importer", []), new("converter", [])])
        {
            AccountScope = new("individualDeveloper", capabilities),
        };
    }

    private static byte[] Sign(PvadLicensePayload payload, ECDsa key, int? envelopeVersion = null)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PvadLicenseJson.Options);
        return JsonSerializer.SerializeToUtf8Bytes(new PvadSignedNativeLicense(envelopeVersion ?? payload.SchemaVersion,
            "synthetic", PvadLicenseJson.Algorithm, Convert.ToBase64String(bytes),
            Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))),
            PvadLicenseJson.Options);
    }

    private static PvadLicenseAuthority Load(PvadLicensePayload payload)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var authority = new PvadLicenseAuthority(new PvadLicenseVerifier(new Dictionary<string, string>
            { ["synthetic"] = key.ExportSubjectPublicKeyInfoPem() }), TimeProvider.System, payload.ValidFrom.AddYears(-1));
        authority.Load(new MemoryStream(Sign(payload, key)));
        return authority;
    }

    [Fact]
    public void IndividualCanImportDocumentsAndExportOdtWithoutEnablingOtherImporterOrConverterFeatures()
    {
        var authority = Load(Payload("import.docx", "export.odt", "export.pdf"));
        authority.RequireCapability("importer", "import.docx");
        using (authority.Begin(PvadNativeOperation.ConverterExport, capability: "export.odt")) { }
        using (authority.Begin(PvadNativeOperation.PdfSerialization)) { }
        Assert.Equal("PVADLIC016", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.PdfImport)).Code);
        Assert.Equal("PVADLIC016", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.ImageOcr)).Code);
        Assert.Equal("PVADLIC016", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.ConverterExport,
            capability: "export.png")).Code);
        Assert.Equal("PVADLIC016", Assert.Throws<PvadLicenseException>(() => authority.CaptureImporterWorkerConfiguration()).Code);
    }

    [Theory]
    [InlineData("import.pdf")]
    [InlineData("import.svg")]
    [InlineData("image.ocr")]
    [InlineData("export.csv")]
    [InlineData("export.svg")]
    [InlineData("pdf.signature.sign")]
    [InlineData("pdf.redaction.apply")]
    [InlineData("pdf.compliance.convert")]
    [InlineData("pdf.e-invoice.create")]
    [InlineData("pdf.e-invoice.validate")]
    [InlineData("pdf.trust.inspect")]
    [InlineData("import.xls")]
    [InlineData("unknown")]
    public void EvenAValidManufacturerSignatureCannotInventPrivateRights(string capability)
    {
        var error = Assert.Throws<PvadLicenseException>(() => Load(Payload(capability)));
        Assert.Equal("PVADLIC004", error.Code);
    }

    [Fact]
    public void OldProductGrantRemainsUsableWithoutAnAccountScope()
    {
        var authority = Load(Payload() with { SchemaVersion = 1, AccountScope = null });
        using (authority.Begin(PvadNativeOperation.PdfImport)) { }
        using (authority.Begin(PvadNativeOperation.ImageOcr)) { }
        using (authority.Begin(PvadNativeOperation.ConverterExport)) { }
    }

    [Fact]
    public void NewLicenseCannotOmitScopeOrDowngradeItsEnvelope()
    {
        Assert.Throws<PvadLicenseException>(() => Load(Payload("export.pdf") with { AccountScope = null }));
        Assert.Throws<PvadLicenseException>(() => Load(Payload("export.pdf") with { SchemaVersion = 1 }));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = new PvadLicenseVerifier(new Dictionary<string, string> { ["synthetic"] = key.ExportSubjectPublicKeyInfoPem() });
        Assert.Equal("PVADLIC005", Assert.Throws<PvadLicenseException>(() => verifier.Verify(Sign(Payload("export.pdf"), key, 1))).Code);
    }

    [Fact]
    public void RemovingScopeAfterSigningInvalidatesTheSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = new PvadLicenseVerifier(new Dictionary<string, string> { ["synthetic"] = key.ExportSubjectPublicKeyInfoPem() });
        var signed = JsonSerializer.Deserialize<PvadSignedNativeLicense>(Sign(Payload("export.pdf"), key), PvadLicenseJson.Options)!;
        var changed = signed with { Payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(
            Payload() with { SchemaVersion = 1, AccountScope = null }, PvadLicenseJson.Options)) };
        Assert.Equal("PVADLIC007", Assert.Throws<PvadLicenseException>(() => verifier.Verify(
            JsonSerializer.SerializeToUtf8Bytes(changed, PvadLicenseJson.Options))).Code);
    }

    [Fact]
    public void InvalidReplacementDoesNotDiscardTheCurrentScopedLicense()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var authority = new PvadLicenseAuthority(new PvadLicenseVerifier(new Dictionary<string, string>
            { ["synthetic"] = key.ExportSubjectPublicKeyInfoPem() }), TimeProvider.System, DateTimeOffset.UtcNow.AddYears(-1));
        var valid = Payload("export.pdf");
        authority.Load(new MemoryStream(Sign(valid, key)));
        Assert.Throws<PvadLicenseException>(() => authority.Load(new MemoryStream(Sign(Payload("export.png"), key))));
        Assert.Equal(valid.LicenseId, authority.GetStatus().LicenseId);
        using (authority.Begin(PvadNativeOperation.PdfSerialization)) { }
    }

    [Fact]
    public void MigrationApprovalRequiresBothSignedCapabilityAndProviderGrant()
    {
        var payload = Payload("migration.pdf.code") with { Grants = [new("migration.pdf.code", ["aspose-pdf"])] };
        var authority = Load(payload);
        using (authority.Begin(PvadNativeOperation.PdfCodeMigration, "aspose-pdf")) { }
        Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.PdfCodeMigration, "itext7"));
        Assert.Throws<PvadLicenseException>(() => Load(payload with { AccountScope = new("individualDeveloper", ["export.pdf"]) }));
        Assert.Throws<PvadLicenseException>(() => Load(Payload("migration.pdf.code")));
    }

    [Fact]
    public void WebsiteScopeRestrictsEvenALegacyCompanyHostLicense()
    {
        var authority = Load(Payload() with { SchemaVersion = 1, AccountScope = null });
        using (PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.IndividualDeveloper))
        {
            Assert.Equal("PVAD_ACCOUNT_CAPABILITY_DENIED", Assert.Throws<PvadLicenseException>(() =>
                authority.Begin(PvadNativeOperation.PdfImport)).Code);
            using (authority.Begin(PvadNativeOperation.ConverterExport, capability: "export.odt")) { }
            using (PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.Company))
                Assert.False(PvadAccountCapabilityContext.IsAllowed("export.png"));
        }
        using (authority.Begin(PvadNativeOperation.PdfImport)) { }
    }

    [Fact]
    public void WorkerRestrictionSnapshotCannotWidenItsParentScope()
    {
        using var scope = PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.IndividualDeveloper);
        var captured = PvadAccountCapabilityContext.Capture()!;
        Assert.Contains("export.pdf", captured);
        Assert.DoesNotContain("image.ocr", captured);
        using var child = PvadAccountCapabilityContext.Restore([.. captured, "image.ocr", "unknown"]);
        Assert.False(PvadAccountCapabilityContext.IsAllowed("image.ocr"));
        Assert.False(PvadAccountCapabilityContext.IsAllowed("unknown"));
    }

    [Fact]
    public async Task EscapedAsyncWorkCannotReuseAClosedAccountScope()
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> child;
        using (PvadAccountCapabilityContext.Enter(PvadLicenseAccountKind.IndividualDeveloper))
            child = Task.Run(async () => { await ready.Task; return PvadAccountCapabilityContext.IsAllowed("export.pdf"); });
        ready.SetResult();
        Assert.False(await child);
    }
}
