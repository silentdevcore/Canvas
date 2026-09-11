using System.Text;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Trust;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfTrustInspectorTests
{
    [Fact]
    public async Task InspectAsync_ValidatesGeneratedPdfA3bStructure()
    {
        var document = new PdfDocument
        {
            FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"))
        };
        document.AddPage().DrawText("PDF/A-3b", 40, 40, new PdfDrawTextOptions { Language = "en" });
        var bytes = document.ToBytes(new PdfSaveOptions
        {
            ComplianceProfile = PdfComplianceProfile.PdfA3B
        });
        if (Environment.GetEnvironmentVariable("PXA_PDFA_ARTIFACT_DIR") is { Length: > 0 } artifactDirectory)
        {
            Directory.CreateDirectory(artifactDirectory);
            await File.WriteAllBytesAsync(Path.Combine(artifactDirectory, "pxa-generated-pdfa3b-fixture.pdf"), bytes);
        }

        var report = await InspectAsync(bytes);

        Assert.Equal("PDF/A-3b", report.PdfA.Profile);
        Assert.Equal(PdfTrustStatus.Valid, report.PdfA.Status);
        Assert.Contains("<pdfaid:part>3</pdfaid:part>", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectAsync_ReportsStructureHashAndNoTrustClaimsForGeneratedPdf()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Trust baseline", 40, 40);

        var report = await InspectAsync(document.ToBytes());

        Assert.Equal(PdfTrustStatus.Indeterminate, report.Status);
        Assert.Equal(PdfTrustStatus.Valid, report.StructureStatus);
        Assert.Equal("1.4", report.PdfVersion);
        Assert.Equal(64, report.Sha256.Length);
        Assert.True(report.ObjectCount > 0);
        Assert.Equal(report.RevisionCount, report.Revisions.Count);
        Assert.All(report.Revisions, revision => Assert.Equal(64, revision.Sha256.Length));
        Assert.False(report.Encryption.IsEncrypted);
        Assert.Empty(report.Signatures);
        Assert.False(report.PdfA.IsClaimed);
        Assert.False(report.PdfUa.IsClaimed);
    }

    [Fact]
    public async Task InspectAsync_ReportsLegacyEncryptionWithoutClaimingValidation()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Encrypted", 40, 40);
        var bytes = document.ToBytes(new PdfSaveOptions
        {
            Encryption = new PdfEncryptionOptions
            {
                UserPassword = "reader",
                OwnerPassword = "owner",
                Algorithm = PdfEncryptionAlgorithm.Rc4_128
            }
        });

        var report = await InspectAsync(bytes);

        Assert.True(report.Encryption.IsEncrypted);
        Assert.Equal(PdfTrustStatus.Indeterminate, report.Encryption.Status);
        Assert.Equal("Standard", report.Encryption.Filter);
        Assert.Equal("RC4", report.Encryption.Algorithm);
        Assert.Equal(2, report.Encryption.Version);
        Assert.Equal(3, report.Encryption.Revision);
        Assert.Equal(128, report.Encryption.KeyLengthBits);
    }

    [Fact]
    public async Task InspectAsync_ReportsModernAes256Encryption()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Encrypted", 40, 40);
        var bytes = document.ToBytes(new PdfSaveOptions
        {
            Encryption = new PdfEncryptionOptions { UserPassword = "reader" }
        });

        var report = await InspectAsync(bytes);

        Assert.True(report.Encryption.IsEncrypted);
        Assert.Equal("AES-256", report.Encryption.Algorithm);
        Assert.Equal(5, report.Encryption.Version);
        Assert.Equal(6, report.Encryption.Revision);
        Assert.Equal(256, report.Encryption.KeyLengthBits);
        Assert.Equal("2.0", report.PdfVersion);
    }

    [Fact]
    public async Task InspectAsync_ReportsMetadataClaimsAsIndeterminate()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Claimed compliance", 40, 40);
        var source = document.ToBytes();
        var marker = Encoding.ASCII.GetBytes(
            "\n<!-- <pdfaid:part>2</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance><pdfuaid:part>1</pdfuaid:part> -->\n");
        var bytes = source.Concat(marker).ToArray();

        var report = await InspectAsync(bytes);

        Assert.Equal("PDF/A-2b", report.PdfA.Profile);
        Assert.Equal(PdfTrustStatus.Indeterminate, report.PdfA.Status);
        Assert.Equal("PDF/UA-1", report.PdfUa.Profile);
        Assert.Equal(PdfTrustStatus.Indeterminate, report.PdfUa.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST301");
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST401");
    }

    [Fact]
    public async Task InspectAsync_InventoriesActiveAndEmbeddedContent()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Risk inventory", 40, 40);
        var source = document.ToBytes();
        var nextObject = NextObjectNumber(source);
        var bytes = new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(new PdfObjectId(nextObject, 0), new PdfDictionary
            {
                ["Type"] = new PdfName("Action"),
                ["S"] = new PdfName("JavaScript"),
                ["JS"] = new PdfString("app.alert('test')"u8.ToArray(), IsHex: false)
            }),
            new PdfIncrementalObjectUpdate(new PdfObjectId(nextObject + 1, 0), new PdfDictionary
            {
                ["Type"] = new PdfName("Filespec"),
                ["F"] = new PdfString("evidence.txt"u8.ToArray(), IsHex: false),
                ["EF"] = new PdfDictionary()
            })
        ]).Bytes;

        var report = await InspectAsync(bytes);

        Assert.NotNull(report.Risk);
        Assert.Equal(1, report.Risk.JavaScriptActionCount);
        Assert.Equal(1, report.Risk.EmbeddedFileCount);
        Assert.True(report.Risk.HasRiskyContent);
        Assert.Equal(PdfTrustStatus.Indeterminate, report.Risk.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST020");
    }

    [Fact]
    public async Task InspectAsync_ValidatesGeneratedPdfA2bStructure()
    {
        var document = new PdfDocument
        {
            FontLoader = new PdfFontLoader(Path.Combine(AppContext.BaseDirectory, "fonts"))
        };
        document.Info.Title = "Archival fixture";
        document.Info.Author = "PXA QA";
        document.Info.Subject = "PDF/A-2b interoperability fixture";
        document.Info.Keywords = "PXA, PDF/A";
        document.Info.Creator = "PXA tests";
        document.Info.Producer = "PXA";
        document.Info.CreationDate = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        document.Info.ModificationDate = new DateTimeOffset(2026, 8, 31, 10, 30, 0, TimeSpan.Zero);
        document.AddPage().DrawText("Embedded archival text", 40, 40, new PdfDrawTextOptions
        {
            Language = "en"
        });

        var bytes = document.ToBytes(new PdfSaveOptions
        {
            ComplianceProfile = PdfComplianceProfile.PdfA2B
        });
        if (Environment.GetEnvironmentVariable("PXA_PDFA_ARTIFACT_DIR") is { Length: > 0 } artifactDirectory)
        {
            Directory.CreateDirectory(artifactDirectory);
            await File.WriteAllBytesAsync(
                Path.Combine(artifactDirectory, "pxa-pdfa2b-fixture.pdf"),
                bytes);
        }
        var report = await InspectAsync(bytes);

        Assert.Equal("1.7", report.PdfVersion);
        Assert.Equal("%PDF-1.7\n%\u00e2\u00e3\u00cf\u00d3\n", Encoding.Latin1.GetString(bytes, 0, 15));
        Assert.Contains("/CIDToGIDMap /Identity", Encoding.Latin1.GetString(bytes));
        Assert.Contains("/ID [<", Encoding.Latin1.GetString(bytes));
        Assert.True(report.PdfA.IsClaimed);
        Assert.Equal("PDF/A-2b", report.PdfA.Profile);
        Assert.Equal(PdfTrustStatus.Valid, report.PdfA.Status);
        Assert.Equal("PXA PDF/A-2b/3b structural validator 1.3", report.PdfA.Validator);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST300");
    }

    [Fact]
    public void PdfA2bGeneration_FailsClosedForUnembeddedFonts()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Standard font", 40, 40);

        var exception = Assert.Throws<PdfComplianceException>(() => document.ToBytes(new PdfSaveOptions
        {
            ComplianceProfile = PdfComplianceProfile.PdfA2B
        }));

        Assert.Contains(exception.Report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFA003");
    }

    [Fact]
    public void PdfA2bGeneration_FailsClosedForCustomDocumentProperties()
    {
        var document = new PdfDocument();
        document.Info.CustomProperties["Customer"] = "Example";

        var exception = Assert.Throws<PdfComplianceException>(() => document.ToBytes(new PdfSaveOptions
        {
            ComplianceProfile = PdfComplianceProfile.PdfA2B
        }));

        Assert.Contains(exception.Report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFA006");
    }

    [Fact]
    public void PdfA2bGeneration_FailsClosedForEncryption()
    {
        var document = new PdfDocument();
        document.AddPage();

        var exception = Assert.Throws<PdfComplianceException>(() => document.ToBytes(new PdfSaveOptions
        {
            ComplianceProfile = PdfComplianceProfile.PdfA2B,
            Encryption = new PdfEncryptionOptions { UserPassword = "reader" }
        }));

        Assert.Contains(exception.Report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFA002");
    }

    [Fact]
    public async Task InspectAsync_RejectsInputAboveConfiguredLimit()
    {
        await using var stream = new MemoryStream("%PDF-1.7 oversized"u8.ToArray());
        var inspector = new PdfTrustInspector();

        await Assert.ThrowsAsync<InvalidDataException>(() => inspector.InspectAsync(
            stream,
            new PdfTrustInspectionOptions { MaximumFileSizeBytes = 8 }));
    }

    [Fact]
    public async Task InspectAsync_RejectsMalformedSignatureByteRange()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Unsigned signature fixture", 40, 40);
        var source = document.ToBytes();
        // The signature object is appended through a real incremental update so that the
        // cross-reference data points at it; an object a reader cannot resolve is covered by
        // InspectAsync_RejectsObjectDefinitionsNoRevisionReferences instead.
        var signatureId = new PdfObjectId(NextObjectNumber(source), 0);
        var bytes = new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(signatureId, new PdfDictionary
            {
                ["Type"] = new PdfName("Sig"),
                ["Filter"] = new PdfName("Adobe.PPKLite"),
                ["SubFilter"] = new PdfName("adbe.pkcs7.detached"),
                ["ByteRange"] = new PdfArray([
                    new PdfInteger(0), new PdfInteger(10), new PdfInteger(20), new PdfInteger(30)
                ]),
                ["Contents"] = new PdfString("01020304"u8.ToArray(), IsHex: true)
            })
        ]).Bytes;

        var report = await InspectAsync(bytes);

        var signature = Assert.Single(report.Signatures);
        Assert.Equal(signatureId.Number, signature.ObjectNumber);
        Assert.Equal(PdfTrustStatus.Invalid, signature.Status);
        Assert.Equal(PdfTrustStatus.Invalid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.CoverageStatus);
        Assert.Equal("adbe.pkcs7.detached", signature.SubFilter);
        Assert.Equal([0L, 10L, 20L, 30L], signature.ByteRange);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST102");
    }

    [Fact]
    public async Task InspectAsync_RejectsObjectDefinitionsNoRevisionReferences()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("Unsigned signature fixture", 40, 40);
        var source = document.ToBytes();
        var shadowObject = Encoding.ASCII.GetBytes(
            "\n999 0 obj\n<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 10 20 30] /Contents <01020304> >>\nendobj\n");

        var report = await InspectAsync(source.Concat(shadowObject).ToArray());

        // A reader resolves nothing here, so PXA must not present the appended bytes as a
        // signature; it reports them as an unreferenced definition instead.
        Assert.Empty(report.Signatures);
        Assert.Equal(PdfTrustStatus.Invalid, report.StructureStatus);
        var diagnostic = Assert.Single(
            report.Diagnostics,
            candidate => candidate.Code == "PXAPDFTRUST150");
        Assert.Equal(999, diagnostic.ObjectNumber);
    }

    private static int NextObjectNumber(byte[] source)
    {
        var context = new PdfParseContext(source, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return table.Entries.Keys.Max(static id => id.Number) + 1;
    }

    private static async Task<PdfTrustReport> InspectAsync(byte[] bytes)
    {
        await using var stream = new MemoryStream(bytes);
        return await new PdfTrustInspector().InspectAsync(stream);
    }
}
