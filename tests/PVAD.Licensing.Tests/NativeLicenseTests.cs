using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PVAD.Licensing;

namespace PVAD.Licensing.Tests;

public sealed class NativeLicenseTests : IDisposable
{
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly Clock clock = new(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly DateTimeOffset releasedAt = new(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PublicApiHasNoDefaultLicenseOrRuntimeTrustInjection()
    {
        Assert.Equal(PvadLicenseState.Unlicensed, PvadLicense.GetStatus().State);
        Assert.Equal("PVADLIC001", Assert.Throws<PvadLicenseException>(() => PvadLicense.Require("pdf")).Code);
        Assert.DoesNotContain(typeof(PvadLicense).GetMethods(), method => method.GetParameters().Any(parameter =>
            parameter.Name?.Contains("key", StringComparison.OrdinalIgnoreCase) == true));
        using var stream = Document(Payload());
        Assert.Equal("PVADLIC006", Assert.Throws<PvadLicenseException>(() => PvadLicense.LoadFromStream(stream)).Code);
        Assert.True(stream.CanRead);
        Assert.Empty(typeof(PvadLicenseOperation).GetConstructors());
        Assert.All(typeof(PvadLicenseOperation).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void ValidFileAdmitsOnlyLicensedProductsAndCanonicalProviders()
    {
        var authority = Authority();
        var payload = Payload() with { Grants = [new("pdf", []), new("migration.pdf.code", ["nutrient"])] };
        using var stream = Document(payload);
        Assert.Equal(PvadLicenseState.Active, authority.Load(stream).State);
        Assert.Equal(payload.LicenseId, authority.Require("pdf", null).LicenseId);
        Assert.Equal("nutrient", authority.Require("migration.pdf.code", "nutrient").Provider);
        Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Require("word", null)).Code);
        Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Require("migration.pdf.code", "Nutrient")).Code);
        Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Require("migration.pdf.code", "aspose-pdf")).Code);
        Assert.Throws<ArgumentException>(() => authority.Require("migration.pdf.code", null));
        Assert.Throws<ArgumentException>(() => authority.Require("pdf", "nutrient"));
        Assert.Throws<ArgumentException>(() => authority.Require("unknown", null));
    }

    [Fact]
    public void ImporterWorkerSnapshotRequiresRightsAndReturnsIndependentVerifiedBytes()
    {
        var authority = Authority();
        Assert.Equal("PVADLIC001", Assert.Throws<PvadLicenseException>(() => authority.CaptureImporterWorkerLicense()).Code);
        using var wrong = Document(Payload());
        authority.Load(wrong);
        Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.CaptureImporterWorkerLicense()).Code);
        var importer = Payload() with { Grants = [new("importer", [])] };
        using var original = Document(importer);
        var expected = original.ToArray();
        authority.Load(original);
        var exported = authority.CaptureImporterWorkerLicense();
        Assert.Equal(expected, exported);
        exported[0] ^= 1;
        Assert.Equal(expected, authority.CaptureImporterWorkerLicense());
        using var invalid = new MemoryStream("{}"u8.ToArray());
        Assert.Throws<PvadLicenseException>(() => authority.Load(invalid));
        Assert.Equal(expected, authority.CaptureImporterWorkerLicense());
        clock.Now = importer.ValidUntil!.Value.AddDays(7);
        Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.CaptureImporterWorkerLicense()).Code);
    }

    [Fact]
    public async Task ImporterWorkerSnapshotRemainsAtomicDuringConcurrentRenewal()
    {
        var authority = Authority();
        var a = Payload() with { Grants = [new("importer", [])] };
        var b = a with { LicenseId = Guid.NewGuid(), CustomerReference = "renewed-synthetic" };
        using var first = Document(a);
        using var second = Document(b);
        var bytesA = first.ToArray();
        var bytesB = second.ToArray();
        authority.Load(first);
        await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            using var replacement = new MemoryStream(i % 2 == 0 ? bytesA : bytesB);
            authority.Load(replacement);
            var snapshot = authority.CaptureImporterWorkerLicense();
            Assert.True(snapshot.SequenceEqual(bytesA) || snapshot.SequenceEqual(bytesB));
            Assert.Contains(Verifier().Verify(snapshot).LicenseId, new[] { a.LicenseId, b.LicenseId });
        })));
    }

    [Fact]
    public void InternalContinuationSurvivesExpiryButNewAdmissionsStayFresh()
    {
        var authority = Authority();
        var payload = Payload() with { Model = PvadLicenseModel.Trial, Grants = [new("importer", [])] };
        using var license = Document(payload);
        authority.Load(license);
        using (authority.Begin(PvadNativeOperation.ImageAnalysis))
        {
            clock.Now = payload.ValidUntil!.Value;
            Assert.Equal(payload.LicenseId, PvadLicenseExecution.Demand("importer").LicenseId);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require("importer", null)).Code);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.ImageAnalysis)).Code);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.CaptureImporterWorkerLicense()).Code);
            Assert.Equal(payload.LicenseId, PvadLicenseExecution.Demand("importer").LicenseId);
        }
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer"));
    }

    [Fact]
    public void AdmittedEditingRetainsOnlyBlankPageDependencyAfterTrialExpiry()
    {
        var authority = Authority();
        var payload = Payload() with { Model = PvadLicenseModel.Trial, Grants = [new("importer", [])] };
        using var license = Document(payload);
        authority.Load(license);
        using (authority.Begin(PvadNativeOperation.PdfEditing))
        {
            clock.Now = payload.ValidUntil!.Value;
            Assert.Equal(payload.LicenseId, PvadLicenseExecution.DemandDependency(PvadNativeDependency.BlankPagePdf).LicenseId);
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(PvadNativeDependency.RegenerationPdf));
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(PvadNativeDependency.OcrResultPdf));
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.PdfEditing)).Code);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require("importer", null)).Code);
            Assert.Throws<InvalidOperationException>(() => PvadLicense.DemandDependency(PvadNativeDependency.BlankPagePdf));
        }
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(PvadNativeDependency.BlankPagePdf));
    }

    [Fact]
    public void AdmittedRegenerationRetainsOnlyItsPrivatePdfDependencyAfterTrialExpiry()
    {
        var authority = Authority();
        var payload = Payload() with { Model = PvadLicenseModel.Trial, Grants = [new("importer", [])] };
        using var license = Document(payload);
        authority.Load(license);
        using (authority.Begin(PvadNativeOperation.ImporterRegeneration))
        {
            clock.Now = payload.ValidUntil!.Value;
            Assert.Equal(payload.LicenseId, PvadLicenseExecution.DemandDependency(PvadNativeDependency.RegenerationPdf).LicenseId);
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(PvadNativeDependency.BlankPagePdf));
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(PvadNativeDependency.OcrResultPdf));
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.ImporterRegeneration)).Code);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require("pdf", null)).Code);
            Assert.Throws<InvalidOperationException>(() => PvadLicense.DemandDependency(PvadNativeDependency.RegenerationPdf));
        }
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(PvadNativeDependency.RegenerationPdf));
    }

    [Fact]
    public void AdmittedTrustOperationsSurviveExpiryWithoutDependencyOrNewOperationRights()
    {
        foreach (var kind in new[] { PvadNativeOperation.PdfTrustInspection, PvadNativeOperation.PdfChangeImpact,
            PvadNativeOperation.PdfSigning, PvadNativeOperation.PdfTimestamp, PvadNativeOperation.PdfRedaction,
            PvadNativeOperation.PdfRedactionAudit, PvadNativeOperation.PdfColorResolution,
            PvadNativeOperation.PdfEInvoice, PvadNativeOperation.PdfAssociatedFiles,
            PvadNativeOperation.PdfArchiveConversion, PvadNativeOperation.PdfColorConversion,
            PvadNativeOperation.PdfTrustPostProcessing })
        {
            var authority = Authority();
            var payload = Payload() with { Model = PvadLicenseModel.Trial, Grants = [new("importer", [])] };
            using var license = Document(payload);
            authority.Load(license);
            using (authority.Begin(kind))
            {
                clock.Now = payload.ValidUntil!.Value;
                Assert.Equal(payload.LicenseId, PvadLicenseExecution.Demand("importer").LicenseId);
                foreach (var dependency in Enum.GetValues<PvadNativeDependency>())
                    Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(dependency));
                Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Begin(kind)).Code);
                Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require("importer", null)).Code);
                Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.CaptureImporterWorkerLicense()).Code);
                Assert.Throws<InvalidOperationException>(() => PvadLicense.DemandContinuation("importer"));
            }
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer"));
        }
    }

    [Fact]
    public void DependencyPolicyIsPurposeBoundAndDoesNotGrantPublicProductAccess()
    {
        var authority = Authority();
        using var license = Document(Payload() with { Grants = [new("importer", [])] });
        authority.Load(license);
        var allowed = new Dictionary<PvadNativeOperation, PvadNativeDependency>
        {
            [PvadNativeOperation.ImporterRegeneration] = PvadNativeDependency.RegenerationPdf,
            [PvadNativeOperation.ImporterBlankPage] = PvadNativeDependency.BlankPagePdf,
            [PvadNativeOperation.PdfEditing] = PvadNativeDependency.BlankPagePdf,
            [PvadNativeOperation.ImageOcr] = PvadNativeDependency.OcrResultPdf,
        };
        foreach (var kind in Enum.GetValues<PvadNativeOperation>().Where(k => PvadLicenseExecution.ProductFor(k) == PvadLicenseProducts.Importer))
        {
            using var scope = authority.Begin(kind);
            foreach (var dependency in Enum.GetValues<PvadNativeDependency>())
            {
                if (allowed.TryGetValue(kind, out var expected) && expected == dependency)
                    Assert.Equal("importer", PvadLicenseExecution.DemandDependency(dependency).Product);
                else
                    Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(dependency));
            }
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("pdf"));
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Require("pdf", null)).Code);
            // A private test authority must not influence the public process-wide verifier.
            Assert.Equal("PVADLIC001", Assert.Throws<PvadLicenseException>(() => PvadLicense.Require("importer")).Code);
            Assert.Equal("PVADLIC001", Assert.Throws<PvadLicenseException>(() => PvadLicense.Require("pdf")).Code);
            Assert.Throws<InvalidOperationException>(() => PvadLicense.DemandContinuation("importer"));
            Assert.Throws<InvalidOperationException>(() => PvadLicense.DemandDependency(PvadNativeDependency.OcrResultPdf));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => authority.Begin((PvadNativeOperation)999));
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(PvadNativeDependency.OcrResultPdf));
    }

    [Fact]
    public void SpreadsheetContinuationCannotAcquireOtherProductsOrPrivatePdfDependencies()
    {
        var authority = Authority();
        Assert.Equal("spreadsheet", PvadLicenseExecution.ProductFor(PvadNativeOperation.SpreadsheetProcessing));
        Assert.Equal("PVADLIC001", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.SpreadsheetProcessing)).Code);
        using var wrong = Document(Payload() with { Grants = [new("importer", [])] });
        authority.Load(wrong);
        Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.SpreadsheetProcessing)).Code);
        var payload = Payload() with { Model = PvadLicenseModel.Trial, Grants = [new("spreadsheet", [])] };
        using var license = Document(payload);
        authority.Load(license);
        using (authority.Begin(PvadNativeOperation.SpreadsheetProcessing))
        {
            Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Require("pdf", null)).Code);
            foreach (var product in new[] { "pdf", "word", "importer", "converter" })
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand(product));
            foreach (var dependency in Enum.GetValues<PvadNativeDependency>())
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(dependency));
            Assert.Throws<InvalidOperationException>(() => PvadLicense.DemandContinuation("spreadsheet"));
            clock.Now = payload.ValidUntil!.Value;
            Assert.Equal(payload.LicenseId, PvadLicenseExecution.Demand("spreadsheet").LicenseId);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require("spreadsheet", null)).Code);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.SpreadsheetProcessing)).Code);
        }
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("spreadsheet"));
    }

    [Fact]
    public void ConverterContinuationCannotAcquireOtherProductsOrPrivatePdfDependencies()
    {
        var authority = Authority();
        Assert.Equal("converter", PvadLicenseExecution.ProductFor(PvadNativeOperation.ConverterExport));
        Assert.Equal("PVADLIC001", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.ConverterExport)).Code);
        using var wrong = Document(Payload());
        authority.Load(wrong);
        Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.ConverterExport)).Code);
        var payload = Payload() with { Model = PvadLicenseModel.Trial, Grants = [new("converter", [])] };
        using var license = Document(payload);
        authority.Load(license);
        using (authority.Begin(PvadNativeOperation.ConverterExport))
        {
            foreach (var product in new[] { "pdf", "word", "spreadsheet", "importer" })
            {
                Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Require(product, null)).Code);
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand(product));
            }
            foreach (var dependency in Enum.GetValues<PvadNativeDependency>())
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(dependency));
            Assert.Throws<InvalidOperationException>(() => PvadLicense.DemandContinuation("converter"));
            clock.Now = payload.ValidUntil!.Value;
            Assert.Equal(payload.LicenseId, PvadLicenseExecution.Demand("converter").LicenseId);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require("converter", null)).Code);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.ConverterExport)).Code);
        }
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("converter"));
    }

    [Theory]
    [InlineData("migration.pdf.code", "aspose-pdf", "nutrient")]
    [InlineData("migration.spreadsheet.code", "aspose-cells", "closedxml")]
    [InlineData("migration.pdf.viewer", "nutrient-viewer", "other-viewer")]
    [InlineData("migration.pdf.designer", "nutrient-viewer", "other-designer")]
    [InlineData("migration.report.designer", "rdl", "rpx")]
    public void CodeContinuationsAreProviderAndAuthorityBoundWithoutRenderingRights(string product, string provider, string other)
    {
        var operation = product switch
        {
            "migration.pdf.code" => PvadNativeOperation.PdfCodeMigration,
            "migration.spreadsheet.code" => PvadNativeOperation.SpreadsheetCodeMigration,
            "migration.pdf.viewer" => PvadNativeOperation.PdfViewerMigration,
            "migration.report.designer" => PvadNativeOperation.ReportDesignerMigration,
            _ => PvadNativeOperation.PdfDesignerMigration,
        };
        var authority = Authority();
        Assert.Throws<ArgumentException>(() => authority.Begin(operation));
        Assert.Equal("PVADLIC001", Assert.Throws<PvadLicenseException>(() => authority.Begin(operation, provider)).Code);
        var payload = Payload() with { Model = PvadLicenseModel.Trial, Grants = [new(product, [provider, other])] };
        using var license = Document(payload);
        authority.Load(license);
        using (authority.Begin(operation, provider))
        {
            Assert.Equal(provider, PvadLicenseExecution.Demand(product, provider: provider).Provider);
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand(product));
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand(product, provider: other));
            Assert.Throws<InvalidOperationException>(() => PvadLicense.DemandMigrationContinuation(product, provider));
            foreach (var dependency in Enum.GetValues<PvadNativeDependency>())
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(dependency));
            foreach (var forbidden in PvadLicenseProducts.All.Where(value => value != product))
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand(forbidden, provider: provider));
            using (authority.Begin(operation, other))
            {
                Assert.Equal(other, PvadLicenseExecution.Demand(product, provider: other).Provider);
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand(product, provider: provider));
            }
            Assert.Equal(provider, PvadLicenseExecution.Demand(product, provider: provider).Provider);
            clock.Now = payload.ValidUntil!.Value;
            Assert.Equal(payload.LicenseId, PvadLicenseExecution.Demand(product, provider: provider).LicenseId);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require(product, provider)).Code);
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Begin(operation, provider)).Code);
        }
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand(product, provider: provider));
    }

    [Fact]
    public void DesignerSourceImportIsPurposeProviderAndAuthorityBoundWithoutPublicEngineRights()
    {
        var authority = Authority();
        var payload = Payload() with { Model = PvadLicenseModel.Trial, Grants = [new("migration.pdf.designer", ["nutrient-viewer", "other-designer"])] };
        using var license = Document(payload);
        authority.Load(license);
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.EnterNutrientDesignerPdfImport(authority));
        using (authority.Begin(PvadNativeOperation.PdfDesignerMigration, "other-designer"))
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.EnterNutrientDesignerPdfImport(authority));
        using (authority.Begin(PvadNativeOperation.PdfDesignerMigration, "nutrient-viewer"))
        {
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.EnterNutrientDesignerPdfImport(new object()));
            Assert.Throws<InvalidOperationException>(() => PvadLicense.BeginNutrientDesignerPdfImport());
            clock.Now = payload.ValidUntil!.Value;
            using (PvadLicenseExecution.EnterNutrientDesignerPdfImport(authority))
            {
                Assert.Equal(payload.LicenseId, PvadLicenseExecution.Demand("importer", authority).LicenseId);
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.EnterNutrientDesignerPdfImport(authority));
                foreach (var dependency in Enum.GetValues<PvadNativeDependency>())
                    Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.DemandDependency(dependency, authority));
                Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require("importer", null)).Code);
                Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require("pdf", null)).Code);
            }
            Assert.Equal(payload.LicenseId, PvadLicenseExecution.Demand("migration.pdf.designer", authority, "nutrient-viewer").LicenseId);
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer", authority));
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.PdfDesignerMigration, "nutrient-viewer")).Code);
        }
        foreach (var purpose in Enum.GetValues<PvadNativeOperation>().Where(value => value != PvadNativeOperation.PdfDesignerMigration))
        {
            var product = PvadLicenseExecution.ProductFor(purpose);
            var admitted = new PvadLicenseOperation(payload.LicenseId, product, PvadLicenseProducts.IsMigration(product) ? "nutrient-viewer" : null, false);
            using var operation = PvadLicenseExecution.Enter(purpose, admitted, authority);
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.EnterNutrientDesignerPdfImport(authority));
        }
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer", authority));
    }

    [Fact]
    public async Task CapturedExecutionContextsAndChildTasksCannotReuseCompletedOperations()
    {
        var authority = Authority();
        using var license = Document(Payload() with { Grants = [new("importer", [])] });
        authority.Load(license);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task child;
        ExecutionContext captured;
        using (authority.Begin(PvadNativeOperation.ImageOcr))
        {
            captured = ExecutionContext.Capture()!;
            child = Task.Run(async () =>
            {
                await release.Task;
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer"));
            });
        }
        release.SetResult();
        await child;
        ExecutionContext.Run(captured, _ =>
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer")), null);
        using (authority.Begin(PvadNativeOperation.ImageOcr))
            Assert.Equal("importer", PvadLicenseExecution.Demand("importer").Product);
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer"));
    }

    [Fact]
    public void NestedFreshOperationsRestoreTheirParentAndEnforceStackDisposal()
    {
        var authority = Authority();
        var importer = Payload() with { Grants = [new("importer", [])] };
        using var first = Document(importer);
        authority.Load(first);
        using var outer = authority.Begin(PvadNativeOperation.ImageOcr);
        var pdf = Payload() with { Grants = [new("pdf", [])] };
        using var replacement = Document(pdf);
        authority.Load(replacement);
        using (var inner = authority.Begin(PvadNativeOperation.PdfSerialization))
        {
            Assert.Throws<InvalidOperationException>(outer.Dispose);
            Assert.Equal(pdf.LicenseId, PvadLicenseExecution.Demand("pdf").LicenseId);
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer"));
        }
        Assert.Equal(importer.LicenseId, PvadLicenseExecution.Demand("importer").LicenseId);
        Assert.Equal("PVADLIC012", Assert.Throws<PvadLicenseException>(() => authority.Begin(PvadNativeOperation.ImageOcr)).Code);
    }

    [Fact]
    public async Task ParallelOperationsKeepIndependentAdmissionsAndCloseTheirOwnContexts()
    {
        var authority = Authority();
        using var license = Document(Payload() with { Grants = [new("pdf", []), new("importer", [])] });
        authority.Load(license);
        await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(async () =>
        {
            var kind = i % 2 == 0 ? PvadNativeOperation.ImageAnalysis : PvadNativeOperation.PdfSerialization;
            var product = PvadLicenseExecution.ProductFor(kind);
            using (authority.Begin(kind))
            {
                await Task.Yield();
                Assert.Equal(product, PvadLicenseExecution.Demand(product).Product);
                Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand(product == "pdf" ? "importer" : "pdf"));
            }
            Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand(product));
        })));
        Assert.Throws<InvalidOperationException>(() => PvadLicenseExecution.Demand("importer"));
    }

    [Theory]
    [InlineData(-1, PvadLicenseState.Active)]
    [InlineData(0, PvadLicenseState.GracePeriod)]
    [InlineData(604799, PvadLicenseState.GracePeriod)]
    [InlineData(604800, PvadLicenseState.Expired)]
    public void SubscriptionHasExactlySevenDaysGrace(int secondsAfterExpiry, PvadLicenseState expected)
    {
        var authority = Authority();
        var payload = Payload();
        using var stream = Document(payload);
        authority.Load(stream);
        clock.Now = payload.ValidUntil!.Value.AddSeconds(secondsAfterExpiry);
        var status = authority.GetStatus();
        Assert.Equal(expected, status.State);
        if (status.CanStartOperation)
            Assert.Equal(expected == PvadLicenseState.GracePeriod, authority.Require("pdf", null).IsGracePeriod);
        else
            Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Require("pdf", null)).Code);
    }

    [Fact]
    public void TrialExpiresWithoutGraceAndAlreadyAdmittedOperationRemainsAValue()
    {
        var authority = Authority();
        var trial = Payload() with { Model = PvadLicenseModel.Trial };
        using var stream = Document(trial);
        authority.Load(stream);
        var admitted = authority.Require("pdf", null);
        clock.Now = trial.ValidUntil!.Value;
        Assert.Equal(PvadLicenseState.Expired, authority.GetStatus().State);
        Assert.Null(authority.GetStatus().GraceEndsAt);
        Assert.Equal(trial.LicenseId, admitted.LicenseId);
        Assert.Throws<PvadLicenseException>(() => authority.Require("pdf", null));
    }

    [Fact]
    public void PerpetualUsesReleaseDateNotCurrentTime()
    {
        var authority = Authority();
        var payload = Payload() with
        {
            Model = PvadLicenseModel.Perpetual, ValidFrom = releasedAt.AddDays(-10),
            IssuedAt = releasedAt.AddDays(-10), ValidUntil = null, UpdatesUntil = releasedAt,
        };
        using var stream = Document(payload);
        authority.Load(stream);
        clock.Now = clock.Now.AddYears(20);
        Assert.Equal(PvadLicenseState.Active, authority.GetStatus().State);
        using var tooOld = Document(payload with { UpdatesUntil = releasedAt.AddTicks(-1) });
        Assert.Equal("PVADLIC011", Assert.Throws<PvadLicenseException>(() => authority.Load(tooOld)).Code);
        Assert.Equal(payload.LicenseId, authority.GetStatus().LicenseId);
    }

    [Fact]
    public void InvalidReloadAndFutureOrExpiredLicensesDoNotReplaceCurrentSnapshot()
    {
        var authority = Authority();
        var payload = Payload();
        using var valid = Document(payload);
        authority.Load(valid);
        using var malformed = new MemoryStream("{}"u8.ToArray());
        Assert.Throws<PvadLicenseException>(() => authority.Load(malformed));
        using var future = Document(payload with { ValidFrom = clock.Now.AddDays(1) });
        Assert.Equal("PVADLIC008", Assert.Throws<PvadLicenseException>(() => authority.Load(future)).Code);
        using var expired = Document(payload with { ValidUntil = clock.Now.AddDays(-8) });
        Assert.Equal("PVADLIC009", Assert.Throws<PvadLicenseException>(() => authority.Load(expired)).Code);
        Assert.Equal(payload.LicenseId, authority.GetStatus().LicenseId);
    }

    [Theory]
    [InlineData("schemaVersion", "2", "PVADLIC005")]
    [InlineData("algorithm", "\"none\"", "PVADLIC005")]
    [InlineData("keyId", "\"untrusted\"", "PVADLIC006")]
    [InlineData("signature", "\"invalid\"", "PVADLIC004")]
    [InlineData("payload", "\"%%%\"", "PVADLIC004")]
    [InlineData("extra", "true", "PVADLIC004")]
    public void RejectsMalformedEnvelope(string property, string json, string code)
    {
        using var document = Document(Payload());
        var node = JsonNode.Parse(document.ToArray())!;
        node[property] = JsonNode.Parse(json);
        Assert.Equal(code, Assert.Throws<PvadLicenseException>(() => Verifier().Verify(JsonSerializer.SerializeToUtf8Bytes(node))).Code);
    }

    [Fact]
    public void RejectsModifiedPayloadAndSignatureAndForeignSigningKey()
    {
        using var stream = Document(Payload());
        var node = JsonNode.Parse(stream.ToArray())!;
        node["payload"] = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(Payload() with { CustomerReference = "changed" }, PvadLicenseJson.Options));
        Assert.Equal("PVADLIC007", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(JsonSerializer.SerializeToUtf8Bytes(node))).Code);
        using var foreign = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var forged = Document(Payload(), signer: foreign);
        Assert.Equal("PVADLIC007", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(forged.ToArray())).Code);
        node["signature"] = Convert.ToBase64String(new byte[64]);
        Assert.Equal("PVADLIC007", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(JsonSerializer.SerializeToUtf8Bytes(node))).Code);
    }

    [Fact]
    public void SignatureCoversOriginalBytesNotReserializedObject()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Payload(), PvadLicenseJson.Options);
        var withWhitespace = Encoding.UTF8.GetBytes(" \n" + Encoding.UTF8.GetString(bytes) + "\n");
        var envelope = Envelope(withWhitespace);
        Assert.Equal(PvadLicenseModel.Subscription, Verifier().Verify(envelope).Model);
        var node = JsonNode.Parse(envelope)!;
        node["payload"] = Convert.ToBase64String(bytes);
        Assert.Equal("PVADLIC007", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(JsonSerializer.SerializeToUtf8Bytes(node))).Code);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"SchemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,}")]
    [InlineData("{/*comment*/\"schemaVersion\":1}")]
    [InlineData("null")]
    public void RejectsDuplicateOrNonStrictJson(string json) =>
        Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(Encoding.UTF8.GetBytes(json))).Code);

    [Fact]
    public void RejectsDuplicateNestedPropertiesInvalidUtf8AndMissingRequiredFields()
    {
        var payload = JsonSerializer.Serialize(Payload(), PvadLicenseJson.Options);
        Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(
            Envelope(Encoding.UTF8.GetBytes(payload.Replace("\"product\":\"pdf\"", "\"product\":\"pdf\",\"product\":\"pdf\""))))).Code);
        Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(Envelope([0xff, 0xfe]))).Code);
        var node = JsonNode.Parse(payload)!;
        foreach (var property in new[] { "model", "redistributionAllowed", "licenseId", "schemaVersion", "grants" })
        {
            var copy = node.DeepClone().AsObject();
            copy.Remove(property);
            Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(
                Envelope(JsonSerializer.SerializeToUtf8Bytes(copy)))).Code);
        }
    }

    [Fact]
    public void EnforcesEnvelopeAndDecodedPayloadBounds()
    {
        Assert.Equal("PVADLIC003", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(new byte[65537])).Code);
        using var stream = new MemoryStream(new byte[65537]);
        Assert.Equal("PVADLIC003", Assert.Throws<PvadLicenseException>(() => Authority().Load(stream)).Code);
        Assert.Equal(65537, stream.Position);
        Assert.Equal("PVADLIC003", Assert.Throws<PvadLicenseException>(() => Verifier().Verify(Envelope(new byte[32769]))).Code);
    }

    [Fact]
    public void KnownKeyRotationWorksButUnsupportedCurveDoesNot()
    {
        using var next = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = new PvadLicenseVerifier(new Dictionary<string, string>
        {
            ["one"] = key.ExportSubjectPublicKeyInfoPem(), ["two"] = next.ExportSubjectPublicKeyInfoPem(),
        });
        using var first = Document(Payload(), "one");
        using var second = Document(Payload(), "two", next);
        verifier.Verify(first.ToArray());
        verifier.Verify(second.ToArray());
        using var wrongCurve = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var wrongVerifier = new PvadLicenseVerifier(new Dictionary<string, string> { ["test"] = wrongCurve.ExportSubjectPublicKeyInfoPem() });
        using var valid = Document(Payload());
        Assert.Equal("PVADLIC007", Assert.Throws<PvadLicenseException>(() => wrongVerifier.Verify(valid.ToArray())).Code);
    }

    [Fact]
    public async Task ConcurrentReplacementNeverExposesPartialGrants()
    {
        var authority = Authority();
        var payload = Payload();
        using var stream = Document(payload);
        authority.Load(stream);
        var bytes = Enumerable.Range(0, 16).Select(_ =>
        {
            using var value = Document(payload with { LicenseId = Guid.NewGuid() });
            return value.ToArray();
        }).ToArray();
        await Task.WhenAll(bytes.Select(data => Task.Run(() =>
        {
            for (var index = 0; index < 25; index++)
            {
                using var input = new MemoryStream(data);
                authority.Load(input);
                Assert.NotEqual(Guid.Empty, authority.Require("pdf", null).LicenseId);
            }
        })));
    }

    [Fact]
    public void PayloadValidationRejectsInvalidTermsAndGrants()
    {
        var good = Payload();
        PvadLicensePayload[] invalid =
        [
            good with { LicenseId = Guid.Empty }, good with { CustomerReference = "\nsecret" },
            good with { SchemaVersion = 3 }, good with { Grants = null! },
            good with { ValidFrom = default },
            good with { ValidUntil = good.ValidFrom }, good with { IssuedAt = good.IssuedAt.ToOffset(TimeSpan.FromHours(1)) },
            good with { ValidFrom = good.ValidFrom.ToOffset(TimeSpan.FromHours(1)) },
            good with { Grants = [new("unknown", [])] }, good with { Grants = [new("pdf", []), new("pdf", [])] },
            good with { Grants = [new("pdf", ["aspose"])] }, good with { Grants = [new("migration.pdf.code", [])] },
            good with { Grants = [new("migration.pdf.code", ["a", "a"])] },
            good with { Grants = [new("migration.pdf.code", ["*"])] },
            good with { Grants = [new("migration.pdf.code", ["Nutrient"])] },
            good with { Model = PvadLicenseModel.Trial, RedistributionAllowed = true },
            good with { Model = PvadLicenseModel.Trial, ValidUntil = good.ValidFrom.AddDays(31) },
            good with { Model = PvadLicenseModel.Perpetual, UpdatesUntil = good.ValidFrom.AddYears(1) },
            good with { UpdatesUntil = good.ValidFrom.AddYears(1) },
            good with { ValidUntil = DateTimeOffset.MaxValue },
        ];
        foreach (var payload in invalid)
        {
            using var stream = Document(payload);
            Assert.Throws<PvadLicenseException>(() => Verifier().Verify(stream.ToArray()));
        }
        Assert.Throws<PvadLicenseException>(() => PvadLicenseVerifier.ValidatePayload(good with { Model = (PvadLicenseModel)15 }));
    }

    [Theory]
    [InlineData("model", "123")]
    [InlineData("model", "\"enterprise\"")]
    [InlineData("customerReference", "null")]
    [InlineData("validUntil", "\"9999-12-31T23:59:59.9999999+00:00\"")]
    public void RejectsSignedInvalidFieldValues(string field, string json)
    {
        var node = JsonSerializer.SerializeToNode(Payload(), PvadLicenseJson.Options)!;
        node[field] = JsonNode.Parse(json);
        Assert.Equal("PVADLIC004", Assert.Throws<PvadLicenseException>(() =>
            Verifier().Verify(Envelope(JsonSerializer.SerializeToUtf8Bytes(node)))).Code);
    }

    [Fact]
    public void FileAndStreamFailuresUseContentFreeDiagnostics()
    {
        Assert.Equal("PVADLIC013", Assert.Throws<PvadLicenseException>(() =>
            PvadLicense.LoadFromFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".missing"))).Code);
        using var disposed = new MemoryStream();
        disposed.Dispose();
        Assert.Equal("PVADLIC013", Assert.Throws<PvadLicenseException>(() => PvadLicense.LoadFromStream(disposed)).Code);
        Assert.Throws<ArgumentNullException>(() => PvadLicense.LoadFromStream(null!));
    }

    [Fact]
    public void BoundedReaderAcceptsNonSeekableStreamsAndDoesNotDisposeThem()
    {
        using var document = Document(Payload());
        using var stream = new NonSeekable(document.ToArray());
        Assert.Equal(PvadLicenseState.Active, Authority().Load(stream).State);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void EveryNativeProductCanBeRepresentedWithoutEditionOrUserRoles()
    {
        var authority = Authority();
        using var stream = Document(Payload() with
        {
            Grants = PvadLicenseProducts.All.Select(product => new PvadLicenseGrant(product,
                PvadLicenseProducts.IsMigration(product) ? ["synthetic-provider"] : [])).ToArray(),
        });
        authority.Load(stream);
        foreach (var product in PvadLicenseProducts.All)
            authority.Require(product, PvadLicenseProducts.IsMigration(product) ? "synthetic-provider" : null);
    }

    [Fact]
    public void ReissuedPerpetualLicensePreservesHistoricalUpdateRights()
    {
        var authority = Authority();
        var payload = Payload() with
        {
            Model = PvadLicenseModel.Perpetual, ValidFrom = releasedAt.AddYears(-1),
            IssuedAt = clock.Now, ValidUntil = null, UpdatesUntil = releasedAt,
        };
        using var stream = Document(payload);
        Assert.Equal(PvadLicenseState.Active, authority.Load(stream).State);
        Assert.Equal(payload.LicenseId, authority.Require("pdf", null).LicenseId);
    }

    [Fact]
    public void FutureIssuanceCannotActivateBeforeItsSignedDate()
    {
        using var stream = Document(Payload() with { IssuedAt = clock.Now.AddDays(1) });
        Assert.Equal("PVADLIC008", Assert.Throws<PvadLicenseException>(() => Authority().Load(stream)).Code);
    }

    private PvadLicensePayload Payload() => new(1, Guid.NewGuid(), "synthetic-customer", PvadLicenseModel.Subscription,
        clock.Now.AddDays(-10), clock.Now.AddDays(-10), clock.Now.AddDays(20), null, false, [new("pdf", [])]);

    private PvadLicenseVerifier Verifier() => new(new Dictionary<string, string> { ["test"] = key.ExportSubjectPublicKeyInfoPem() });
    private PvadLicenseAuthority Authority() => new(Verifier(), clock, releasedAt);
    private MemoryStream Document(PvadLicensePayload payload, string keyId = "test", ECDsa? signer = null) =>
        new(Envelope(JsonSerializer.SerializeToUtf8Bytes(payload, PvadLicenseJson.Options), keyId, signer));

    private byte[] Envelope(byte[] payload, string keyId = "test", ECDsa? signer = null) =>
        JsonSerializer.SerializeToUtf8Bytes(new PvadSignedNativeLicense(1, keyId, PvadLicenseJson.Algorithm,
            Convert.ToBase64String(payload), Convert.ToBase64String((signer ?? key).SignData(payload,
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))), PvadLicenseJson.Options);

    public void Dispose() => key.Dispose();

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class NonSeekable(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }
}
