using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Signing;
using PXA.Importer.Trust;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfPadesSigningServiceTests
{
    [Fact]
    public void CadesBuilder_ShouldCreateVerifiableRsaSignedAttributes()
    {
        using var fixture = SigningFixture.Create();
        var documentDigest = SHA256.HashData("PXA detached content"u8);

        var result = PdfCadesCmsBuilder.CreateDetached(documentDigest, fixture.Key, fixture.SigningTime);

        Assert.Equal(0x30, result.Cms[0]);
        Assert.True(result.SignedAttributes.AsSpan().IndexOf(documentDigest) >= 0);
        Assert.Equal(
            SHA256.HashData(fixture.Key.Certificate.RawData),
            ReadSigningCertificateV2Hash(result.SignedAttributes));
        Assert.True(result.SignedAttributes.AsSpan().IndexOf(
            Convert.FromHexString("06092A864886F70D010905")) >= 0);
        using var publicKey = fixture.Key.Certificate.GetRSAPublicKey();
        Assert.NotNull(publicKey);
        Assert.True(publicKey.VerifyHash(
            SHA256.HashData(result.SignedAttributes),
            result.Signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public async Task Sign_ShouldCreateVerifiablePadesBaselineBRevision()
    {
        using var fixture = SigningFixture.Create();
        var source = CreatePdf();

        var result = new PdfPadesSigningService().Sign(
            source,
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "Approval",
                Reason = "Release approval",
                Location = "Zurich",
                SigningTime = fixture.SigningTime
            });

        Assert.True(result.Bytes.AsSpan(0, source.Length).SequenceEqual(source));
        Assert.Equal(PdfPadesProfile.BaselineB, result.Profile);
        Assert.Equal("SHA-256", result.DigestAlgorithm);

        await using var signedStream = new MemoryStream(result.Bytes);
        var trust = await new PdfTrustInspector().InspectAsync(signedStream);
        var signature = Assert.Single(trust.Signatures);
        Assert.Equal("ETSI.CAdES.detached", signature.SubFilter);
        Assert.Equal(4, signature.ByteRange.Count);
        Assert.Equal(0, signature.ByteRange[0]);
        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Valid, signature.CoverageStatus);
        Assert.Equal(PdfTrustStatus.Indeterminate, signature.CertificateStatus);
        Assert.Equal(PdfTrustStatus.Indeterminate, signature.Status);
        Assert.Equal("B-B", signature.PadesProfile);
        Assert.Equal("SHA-256", signature.DigestAlgorithm);
        Assert.Equal(fixture.Key.Certificate.Subject, signature.SignerSubject);
        Assert.True(signature.CoversWholeDocument);
        Assert.Equal(result.RevisionCount, trust.RevisionCount);

        var parsed = ParseSignature(result.Bytes, signature.ObjectNumber, signature.Generation);
        var documentDigest = HashByteRanges(result.Bytes, signature.ByteRange);
        var cms = parsed.Contents[..ReadDerLength(parsed.Contents)];
        var cmsData = ReadCms(cms);

        Assert.Equal(documentDigest, cmsData.MessageDigest);
        using var publicKey = fixture.Key.Certificate.GetRSAPublicKey();
        Assert.NotNull(publicKey);
        Assert.True(publicKey.VerifyHash(
            SHA256.HashData(cmsData.SignedAttributes),
            cmsData.Signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
        Assert.True(parsed.IsReferencedByAcroForm);

        var externalOutput = Environment.GetEnvironmentVariable("PXA_SIGNED_PDF_OUTPUT");
        if (!string.IsNullOrWhiteSpace(externalOutput))
        {
            await File.WriteAllBytesAsync(externalOutput, result.Bytes);
            await File.WriteAllBytesAsync($"{externalOutput}.p7s", cms);
            await File.WriteAllBytesAsync(
                $"{externalOutput}.content",
                ReadByteRanges(result.Bytes, signature.ByteRange));
        }
    }

    [Theory]
    [InlineData(PdfSignatureDigestAlgorithm.Sha384, "SHA-384")]
    [InlineData(PdfSignatureDigestAlgorithm.Sha512, "SHA-512")]
    public async Task Sign_ShouldUseRequestedStrongDigest(
        PdfSignatureDigestAlgorithm digestAlgorithm,
        string expectedName)
    {
        using var fixture = SigningFixture.Create();
        var result = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions
            {
                DigestAlgorithm = digestAlgorithm,
                SigningTime = fixture.SigningTime
            });

        await using var stream = new MemoryStream(result.Bytes);
        var signature = Assert.Single((await new PdfTrustInspector().InspectAsync(stream)).Signatures);

        Assert.Equal(expectedName, result.DigestAlgorithm);
        Assert.Equal(expectedName, signature.DigestAlgorithm);
        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
    }

    [Fact]
    public async Task Inspect_ShouldTrustSignerFromCustomTrustStoreAlongsideSystemStore()
    {
        using var fixture = SigningFixture.Create();
        var result = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { SigningTime = fixture.SigningTime });

        await using var stream = new MemoryStream(result.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(
            stream,
            new PdfTrustInspectionOptions
            {
                SignatureValidationPolicy = new PdfSignatureValidationPolicy
                {
                    UseSystemTrustStore = true,
                    CustomTrustAnchors = [fixture.Key.Certificate]
                }
            });

        var signature = Assert.Single(report.Signatures);
        Assert.Equal(PdfTrustStatus.Valid, signature.CertificateStatus);
        Assert.Equal(PdfTrustStatus.NotApplicable, signature.RevocationStatus);
        Assert.Equal(PdfTrustStatus.Valid, signature.Status);
    }

    [Fact]
    public async Task Inspect_ShouldBuildChainWithIssuerFromBoundedProvider()
    {
        var now = DateTimeOffset.UtcNow;
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=PXA AIA Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 1, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));

        using var intermediateKey = RSA.Create(2048);
        var intermediateRequest = new CertificateRequest("CN=PXA AIA Intermediate", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var intermediatePublic = intermediateRequest.Create(root, now.AddHours(-1), now.AddDays(1), RandomNumberGenerator.GetBytes(16));
        using var intermediate = intermediatePublic.CopyWithPrivateKey(intermediateKey);

        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=PXA AIA Signer", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using var leafPublic = leafRequest.Create(intermediate, now.AddHours(-1), now.AddHours(12), RandomNumberGenerator.GetBytes(16));
        using var leaf = leafPublic.CopyWithPrivateKey(leafKey);
        using var signingKey = new PdfPkcs12SigningKey(leaf.Export(X509ContentType.Pkcs12, "test"), "test");
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            signingKey,
            new PdfSigningOptions { SigningTime = now });
        var issuerProvider = new TestIssuerCertificateProvider(intermediate.RawData);

        await using var stream = new MemoryStream(signed.Bytes);
        var signature = Assert.Single((await new PdfTrustInspector().InspectAsync(
            stream,
            new PdfTrustInspectionOptions
            {
                SignatureValidationPolicy = new PdfSignatureValidationPolicy
                {
                    UseSystemTrustStore = false,
                    CustomTrustAnchors = [root],
                    AllowCertificateDownloads = true,
                    IssuerCertificateProvider = issuerProvider
                }
            })).Signatures);

        Assert.Equal(1, issuerProvider.CallCount);
        Assert.Equal(PdfTrustStatus.Valid, signature.CertificateStatus);
    }

    [Fact]
    public async Task Inspect_ShouldReportIndeterminateWhenOfflineRevocationEvidenceIsUnavailable()
    {
        var now = DateTimeOffset.UtcNow;
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=PXA Offline Revocation Root",
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        using var root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));

        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest(
            "CN=PXA Offline Revocation Signer",
            leafKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
            true));
        using var leafPublic = leafRequest.Create(
            root,
            now.AddHours(-1),
            now.AddDays(1),
            RandomNumberGenerator.GetBytes(16));
        using var leaf = leafPublic.CopyWithPrivateKey(leafKey);
        using var signingKey = new PdfPkcs12SigningKey(
            leaf.Export(X509ContentType.Pkcs12, "test-password"),
            "test-password");
        var result = new PdfPadesSigningService().Sign(
            CreatePdf(),
            signingKey,
            new PdfSigningOptions { SigningTime = now });

        await using var stream = new MemoryStream(result.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(
            stream,
            new PdfTrustInspectionOptions
            {
                SignatureValidationPolicy = new PdfSignatureValidationPolicy
                {
                    UseSystemTrustStore = false,
                    CustomTrustAnchors = [root],
                    RevocationMode = PdfCertificateRevocationMode.Offline
                }
            });

        var signature = Assert.Single(report.Signatures);
        Assert.Equal(PdfTrustStatus.Valid, signature.CertificateStatus);
        Assert.Equal(PdfTrustStatus.Indeterminate, signature.RevocationStatus);
        Assert.Equal(PdfTrustStatus.Indeterminate, signature.Status);
    }

    [Theory]
    [InlineData(PdfTrustStatus.Valid, PdfTrustStatus.Valid, "PXAPDFTRUST115")]
    [InlineData(PdfTrustStatus.Invalid, PdfTrustStatus.Invalid, "PXAPDFTRUST116")]
    public async Task Inspect_ShouldApplyValidatedApplicationRevocationEvidence(
        PdfTrustStatus evidenceStatus,
        PdfTrustStatus expectedSignatureStatus,
        string expectedDiagnostic)
    {
        var now = DateTimeOffset.UtcNow;
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=PXA Evidence Root",
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        using var root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));

        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest(
            "CN=PXA Evidence Signer",
            leafKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using var leafPublic = leafRequest.Create(root, now.AddHours(-1), now.AddDays(1), RandomNumberGenerator.GetBytes(16));
        using var leaf = leafPublic.CopyWithPrivateKey(leafKey);
        using var signingKey = new PdfPkcs12SigningKey(
            leaf.Export(X509ContentType.Pkcs12, "test-password"),
            "test-password");
        var result = new PdfPadesSigningService().Sign(
            CreatePdf(),
            signingKey,
            new PdfSigningOptions { SigningTime = now });
        var provider = new TestEvidenceProvider(new PdfRevocationEvidence(
            PdfRevocationEvidenceKind.Crl,
            evidenceStatus,
            "configured:test.crl",
            now.AddHours(-1),
            now.AddHours(1),
            [0x30, 0x00]));

        await using var stream = new MemoryStream(result.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(stream, new PdfTrustInspectionOptions
        {
            SignatureValidationPolicy = new PdfSignatureValidationPolicy
            {
                UseSystemTrustStore = false,
                CustomTrustAnchors = [root],
                RevocationMode = PdfCertificateRevocationMode.Offline,
                RevocationEvidenceProvider = provider
            }
        });

        var signature = Assert.Single(report.Signatures);
        Assert.Equal(evidenceStatus, signature.RevocationStatus);
        Assert.Equal(expectedSignatureStatus, signature.Status);
        var evidence = Assert.Single(signature.RevocationEvidence!);
        Assert.Equal(PdfRevocationEvidenceKind.Crl, evidence.Kind);
        Assert.Equal("configured:test.crl", evidence.Source);
        Assert.Equal(1, provider.CallCount);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == expectedDiagnostic);
    }

    [Fact]
    public async Task Sign_ShouldAppendSecondSignatureWithoutChangingFirstRevisionBytes()
    {
        using var fixture = SigningFixture.Create();
        var service = new PdfPadesSigningService();
        var first = service.Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "First", SigningTime = fixture.SigningTime });

        var second = service.Sign(
            first.Bytes,
            fixture.Key,
            new PdfSigningOptions { FieldName = "Second", SigningTime = fixture.SigningTime.AddMinutes(1) });

        Assert.True(second.Bytes.AsSpan(0, first.Bytes.Length).SequenceEqual(first.Bytes));
        await using var stream = new MemoryStream(second.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        Assert.Equal(2, report.Signatures.Count);
        Assert.All(report.Signatures, signature => Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus));
        Assert.DoesNotContain(report.Diagnostics, diagnostic =>
            diagnostic.Code is "PXAPDFTRUST131" or "PXAPDFTRUST132" or "PXAPDFTRUST133" or "PXAPDFTRUST134");
        Assert.False(report.Signatures[0].CoversWholeDocument);
        Assert.True(report.Signatures[1].CoversWholeDocument);
        Assert.Equal(first.RevisionCount + 1, second.RevisionCount);
    }

    [Fact]
    public async Task Inspect_ShouldRejectCryptographicallyModifiedSignedContent()
    {
        using var fixture = SigningFixture.Create();
        var result = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime });
        var modified = result.Bytes.ToArray();
        var textOffset = modified.AsSpan().IndexOf("PXA PAdES fixture"u8);
        Assert.True(textOffset >= 0);
        modified[textOffset] ^= 1;

        await using var stream = new MemoryStream(modified);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        var signature = Assert.Single(report.Signatures);

        Assert.Equal(PdfTrustStatus.Invalid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST105");
    }

    [Fact]
    public async Task Inspect_ShouldRejectUnsignedBytesAfterFinalSignedRevision()
    {
        using var fixture = SigningFixture.Create();
        var result = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime });
        var modified = result.Bytes.Concat("shadow-update"u8.ToArray()).ToArray();

        await using var stream = new MemoryStream(modified);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        var signature = Assert.Single(report.Signatures);

        Assert.Equal(PdfTrustStatus.Invalid, report.StructureStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.CoverageStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.IntegrityStatus);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFXREF005");
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST102");
    }

    [Fact]
    public async Task Inspect_ShouldRejectSignatureObjectShadowedByIncrementalRevision()
    {
        using var fixture = SigningFixture.Create();
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime });
        var (table, graph) = ParseGraph(signed.Bytes);
        var signatureObject = Assert.Single(graph.Objects.Values, item =>
            item.Value is PdfDictionary dictionary && dictionary["Type"] is PdfName { Value: "Sig" });
        var shadowed = new PdfIncrementalUpdateWriter().Append(
            signed.Bytes,
            [new PdfIncrementalObjectUpdate(
                signatureObject.Id,
                new PdfDictionary(((PdfDictionary)signatureObject.Value).Values.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value)))]);

        Assert.Equal(table.Revisions.Count + 1, shadowed.RevisionCount);
        await using var stream = new MemoryStream(shadowed.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        var signature = Assert.Single(report.Signatures);

        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.CoverageStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST133");
    }

    [Fact]
    public async Task Inspect_ShouldRejectSignatureRemovedFromCurrentAcroFormTree()
    {
        using var fixture = SigningFixture.Create();
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime });
        var (_, graph) = ParseGraph(signed.Bytes);
        var catalogReference = Assert.IsType<PdfReference>(graph.Trailer?["Root"]);
        var catalog = Assert.IsType<PdfDictionary>(Resolve(graph, catalogReference));
        var acroForm = Assert.IsType<PdfDictionary>(Resolve(graph, catalog["AcroForm"]));
        var replacement = new PdfDictionary(acroForm.Values.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value));
        replacement["Fields"] = new PdfArray();
        var update = catalog["AcroForm"] is PdfReference acroFormReference
            ? new PdfIncrementalObjectUpdate(acroFormReference.Id, replacement)
            : new PdfIncrementalObjectUpdate(
                catalogReference.Id,
                new PdfDictionary(catalog.Values.ToDictionary(
                    static pair => pair.Key,
                    pair => pair.Key == "AcroForm" ? replacement : pair.Value)));
        var wrapped = new PdfIncrementalUpdateWriter().Append(
            signed.Bytes,
            [update]);

        await using var stream = new MemoryStream(wrapped.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        var signature = Assert.Single(report.Signatures);

        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.CoverageStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST131");
    }

    [Fact]
    public async Task Inspect_ShouldRejectOldSignatureWrappedInLaterField()
    {
        using var fixture = SigningFixture.Create();
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime });
        var (_, graph) = ParseGraph(signed.Bytes);
        var signatureObject = Assert.Single(graph.Objects.Values, item =>
            item.Value is PdfDictionary dictionary && dictionary["Type"] is PdfName { Value: "Sig" });
        var catalogReference = Assert.IsType<PdfReference>(graph.Trailer?["Root"]);
        var catalog = Assert.IsType<PdfDictionary>(Resolve(graph, catalogReference));
        var acroForm = Assert.IsType<PdfDictionary>(Resolve(graph, catalog["AcroForm"]));
        var wrappedFieldId = new PdfObjectId(graph.Objects.Keys.Max(static id => id.Number) + 1, 0);
        var wrappedField = new PdfDictionary
        {
            ["FT"] = new PdfName("Sig"),
            ["T"] = new PdfString("WrappedApproval"u8.ToArray(), IsHex: false),
            ["V"] = new PdfReference(signatureObject.Id)
        };
        var replacement = new PdfDictionary(acroForm.Values.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value));
        replacement["Fields"] = new PdfArray([new PdfReference(wrappedFieldId)]);
        var acroFormUpdate = catalog["AcroForm"] is PdfReference acroFormReference
            ? new PdfIncrementalObjectUpdate(acroFormReference.Id, replacement)
            : new PdfIncrementalObjectUpdate(
                catalogReference.Id,
                new PdfDictionary(catalog.Values.ToDictionary(
                    static pair => pair.Key,
                    pair => pair.Key == "AcroForm" ? replacement : pair.Value)));
        var wrapped = new PdfIncrementalUpdateWriter().Append(
            signed.Bytes,
            [acroFormUpdate, new PdfIncrementalObjectUpdate(wrappedFieldId, wrappedField)]);

        await using var stream = new MemoryStream(wrapped.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        var signature = Assert.Single(report.Signatures);

        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.CoverageStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST134");
    }

    [Theory]
    [InlineData("nonzero-start")]
    [InlineData("overlap")]
    [InlineData("outside-file")]
    [InlineData("unknown-revision")]
    [InlineData("invalid-gap")]
    public async Task Inspect_ShouldRejectUnsafeByteRangeMutations(string mutation)
    {
        using var fixture = SigningFixture.Create();
        var result = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime });
        var modified = RewriteByteRange(result.Bytes, values => mutation switch
        {
            "nonzero-start" => [1, values[1], values[2], values[3]],
            "overlap" => [0, values[2] + 1, values[2], values[3]],
            "outside-file" => [0, values[1], values[2], result.Bytes.LongLength],
            "unknown-revision" => [0, values[1], values[2], values[3] - 1],
            "invalid-gap" => [0, values[1] + 1, values[2], values[3]],
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        });

        await using var stream = new MemoryStream(modified);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        var signature = Assert.Single(report.Signatures);

        Assert.Equal(PdfTrustStatus.Invalid, signature.CoverageStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.IntegrityStatus);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST102");
    }

    [Fact]
    public void Sign_ShouldRejectDuplicateFieldName()
    {
        using var fixture = SigningFixture.Create();
        var service = new PdfPadesSigningService();
        var first = service.Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime });

        var exception = Assert.Throws<InvalidOperationException>(() => service.Sign(
            first.Bytes,
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime.AddMinutes(1) }));

        Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_ShouldCreateVisibleWidgetAppearanceOnRequestedPage()
    {
        using var fixture = SigningFixture.Create();
        var document = new PdfDocument();
        document.AddPage().DrawText("Page one", 40, 40);
        document.AddPage().DrawText("Page two", 40, 40);

        var result = new PdfPadesSigningService().Sign(
            document.ToBytes(),
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "VisibleApproval",
                Reason = "Approved for release",
                SigningTime = fixture.SigningTime,
                Appearance = new PdfSignatureAppearanceOptions
                {
                    PageNumber = 2,
                    X = 72,
                    Y = 80,
                    Width = 220,
                    Height = 72,
                    Title = "PXA approval"
                }
            });

        var widget = ParseWidget(result.Bytes, "VisibleApproval");
        Assert.Equal([72d, 80d, 292d, 152d], widget.Rectangle);
        Assert.Equal("Page", widget.PageType);
        Assert.Contains("PXA approval", widget.AppearanceContent, StringComparison.Ordinal);
        Assert.Contains("PXA PDF Signing Test", widget.AppearanceContent, StringComparison.Ordinal);
        Assert.Contains("Approved for release", widget.AppearanceContent, StringComparison.Ordinal);

        await using var stream = new MemoryStream(result.Bytes);
        var signature = Assert.Single((await new PdfTrustInspector().InspectAsync(stream)).Signatures);
        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
        Assert.Equal("VisibleApproval", signature.FieldName);
        Assert.Equal(2, signature.FieldPageNumber);
        Assert.Equal([72d, 80d, 292d, 152d], signature.FieldRectangle);
    }

    [Fact]
    public async Task SignAsync_ShouldCreatePadesBaselineTWithRfc3161Timestamp()
    {
        using var fixture = SigningFixture.Create();
        using var timestampAuthority = TestTimestampAuthority.Create(fixture.SigningTime.AddSeconds(5));

        var result = await new PdfPadesSigningService().SignAsync(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "TimestampedApproval",
                Profile = PdfPadesProfile.BaselineT,
                SigningTime = fixture.SigningTime
            },
            timestampAuthority);

        Assert.Equal(PdfPadesProfile.BaselineT, result.Profile);
        Assert.Equal(timestampAuthority.Timestamp, result.TimestampTime);
        await using var stream = new MemoryStream(result.Bytes);
        var signature = Assert.Single((await new PdfTrustInspector().InspectAsync(stream)).Signatures);
        Assert.Equal("B-T", signature.PadesProfile);
        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Indeterminate, signature.TimestampStatus);
        AssertTimestampClose(timestampAuthority.Timestamp, signature.TimestampTime);
        Assert.Contains("PXA Test TSA", signature.TimestampAuthoritySubject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignAsync_ShouldCreateAndValidateBaselineLtWithMatchedVri()
    {
        using var fixture = SigningFixture.Create();
        using var timestampAuthority = TestTimestampAuthority.Create(fixture.SigningTime.AddSeconds(5));
        var signed = await new PdfPadesSigningService().SignAsync(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "LongTermApproval",
                Profile = PdfPadesProfile.BaselineLT,
                SigningTime = fixture.SigningTime,
                LongTermValidation = new PdfLongTermValidationMaterial
                {
                    Certificates = [fixture.Key.Certificate.RawData, timestampAuthority.CertificateBytes],
                    TrustAnchors = [fixture.Key.Certificate.RawData, timestampAuthority.CertificateBytes]
                }
            },
            timestampAuthority);

        Assert.Equal(PdfPadesProfile.BaselineLT, signed.Profile);
        Assert.Equal(3, signed.RevisionCount);
        await using var stream = new MemoryStream(signed.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(
            stream,
            new PdfTrustInspectionOptions
            {
                SignatureValidationPolicy = new PdfSignatureValidationPolicy
                {
                    UseSystemTrustStore = false,
                    CustomTrustAnchors = [fixture.Key.Certificate, timestampAuthority.Certificate]
                }
            });

        var signature = Assert.Single(report.Signatures);
        Assert.Equal("B-LT", signature.PadesProfile);
        Assert.Equal(PdfTrustStatus.Valid, signature.EmbeddedValidationMaterial?.Status);
        Assert.Equal(PdfTrustStatus.NotApplicable, signature.HistoricalRevocationStatus);
        Assert.Equal(PdfTrustStatus.NotApplicable, signature.TimestampHistoricalRevocationStatus);
        Assert.False(signature.CoversWholeDocument);
        Assert.Equal(2, report.EmbeddedValidationMaterial?.CertificateCount);
    }

    [Fact]
    public async Task SignAsync_ShouldRejectBaselineLtWithoutValidationMaterial()
    {
        using var fixture = SigningFixture.Create();
        using var timestampAuthority = TestTimestampAuthority.Create(fixture.SigningTime.AddSeconds(5));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PdfPadesSigningService().SignAsync(
                CreatePdf(),
                fixture.Key,
                new PdfSigningOptions
                {
                    Profile = PdfPadesProfile.BaselineLT,
                    SigningTime = fixture.SigningTime
                },
                timestampAuthority));

        Assert.Contains("validation material", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignAsync_ShouldMergeBaselineLtMaterialAcrossMultipleSignatures()
    {
        using var fixture = SigningFixture.Create();
        using var timestampAuthority = TestTimestampAuthority.Create(fixture.SigningTime.AddSeconds(5));
        var material = new PdfLongTermValidationMaterial
        {
            Certificates = [fixture.Key.Certificate.RawData, timestampAuthority.CertificateBytes],
            TrustAnchors = [fixture.Key.Certificate.RawData, timestampAuthority.CertificateBytes]
        };
        var service = new PdfPadesSigningService();
        var first = await service.SignAsync(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "FirstLongTermApproval",
                Profile = PdfPadesProfile.BaselineLT,
                SigningTime = fixture.SigningTime,
                LongTermValidation = material
            },
            timestampAuthority);
        var second = await service.SignAsync(
            first.Bytes,
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "SecondLongTermApproval",
                Profile = PdfPadesProfile.BaselineLT,
                SigningTime = fixture.SigningTime.AddSeconds(1),
                LongTermValidation = material
            },
            timestampAuthority);

        await using var stream = new MemoryStream(second.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(
            stream,
            new PdfTrustInspectionOptions
            {
                SignatureValidationPolicy = new PdfSignatureValidationPolicy
                {
                    UseSystemTrustStore = false,
                    CustomTrustAnchors = [fixture.Key.Certificate, timestampAuthority.Certificate]
                }
            });

        Assert.Equal(5, second.RevisionCount);
        Assert.Equal(2, report.Signatures.Count);
        Assert.All(report.Signatures, signature => Assert.Equal("B-LT", signature.PadesProfile));
        Assert.All(report.Signatures, signature => Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus));
        Assert.Equal(2, report.EmbeddedValidationMaterial?.CertificateCount);
        Assert.Equal(2, report.EmbeddedValidationMaterial?.VriEntryCount);
    }

    [Fact]
    public async Task DocumentTimestamp_ShouldProtectTheCompleteCurrentRevision()
    {
        using var fixture = SigningFixture.Create();
        using var timestampAuthority = TestTimestampAuthority.Create(fixture.SigningTime.AddSeconds(5));

        var result = await new PdfDocumentTimestampService().AddAsync(
            CreatePdf(),
            timestampAuthority,
            new PdfDocumentTimestampOptions { FieldName = "ArchiveTimestamp" });

        await using var stream = new MemoryStream(result.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(
            stream,
            new PdfTrustInspectionOptions
            {
                SignatureValidationPolicy = new PdfSignatureValidationPolicy
                {
                    UseSystemTrustStore = false,
                    CustomTrustAnchors = [timestampAuthority.Certificate]
                }
            });
        var timestamp = Assert.Single(report.Signatures);
        Assert.Equal(2, result.RevisionCount);
        Assert.True(timestamp.IsDocumentTimestamp);
        Assert.Equal("ETSI.RFC3161", timestamp.SubFilter);
        Assert.Equal("DocTimeStamp", timestamp.PadesProfile);
        Assert.Equal(PdfTrustStatus.Valid, timestamp.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Valid, timestamp.TimestampStatus);
        Assert.True(timestamp.CoversWholeDocument);
        AssertTimestampClose(timestampAuthority.Timestamp, timestamp.TimestampTime);
    }

    [Fact]
    public async Task DocumentTimestamp_ShouldDetectChangesToProtectedBytes()
    {
        using var fixture = SigningFixture.Create();
        using var timestampAuthority = TestTimestampAuthority.Create(fixture.SigningTime.AddSeconds(5));
        var result = await new PdfDocumentTimestampService().AddAsync(CreatePdf(), timestampAuthority);
        var textOffset = result.Bytes.AsSpan().IndexOf("PXA PAdES fixture"u8);
        Assert.True(textOffset >= 0);
        result.Bytes[textOffset] ^= 0x01;

        await using var stream = new MemoryStream(result.Bytes);
        var timestamp = Assert.Single((await new PdfTrustInspector().InspectAsync(stream)).Signatures);

        Assert.True(timestamp.IsDocumentTimestamp);
        Assert.Equal(PdfTrustStatus.Invalid, timestamp.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Invalid, timestamp.TimestampStatus);
    }

    [Fact]
    public async Task SignAsync_ShouldCreateBaselineLtaWithArchiveTimestampRevision()
    {
        using var fixture = SigningFixture.Create();
        using var timestampAuthority = TestTimestampAuthority.Create(fixture.SigningTime.AddSeconds(5));

        var result = await new PdfPadesSigningService().SignAsync(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "ArchivedApproval",
                Profile = PdfPadesProfile.BaselineLTA,
                SigningTime = fixture.SigningTime,
                LongTermValidation = new PdfLongTermValidationMaterial
                {
                    Certificates = [fixture.Key.Certificate.RawData, timestampAuthority.CertificateBytes],
                    TrustAnchors = [fixture.Key.Certificate.RawData, timestampAuthority.CertificateBytes]
                }
            },
            timestampAuthority);

        await using var stream = new MemoryStream(result.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(
            stream,
            new PdfTrustInspectionOptions
            {
                SignatureValidationPolicy = new PdfSignatureValidationPolicy
                {
                    UseSystemTrustStore = false,
                    CustomTrustAnchors = [fixture.Key.Certificate, timestampAuthority.Certificate]
                }
            });

        Assert.Equal(PdfPadesProfile.BaselineLTA, result.Profile);
        Assert.Equal(4, result.RevisionCount);
        Assert.Equal(timestampAuthority.Timestamp, result.ArchiveTimestampTime);
        var approval = Assert.Single(report.Signatures, static signature => !signature.IsDocumentTimestamp);
        var archiveTimestamp = Assert.Single(report.Signatures, static signature => signature.IsDocumentTimestamp);
        Assert.Equal("B-LTA", approval.PadesProfile);
        Assert.Equal(PdfTrustStatus.Valid, approval.ArchiveTimestampStatus);
        AssertTimestampClose(timestampAuthority.Timestamp, approval.ArchiveTimestampTime);
        Assert.Equal(PdfTrustStatus.Valid, archiveTimestamp.IntegrityStatus);
        Assert.Equal(PdfTrustStatus.Valid, archiveTimestamp.TimestampStatus);
        Assert.True(archiveTimestamp.CoversWholeDocument);
    }

    [Fact]
    public async Task SignAsync_ShouldRejectTimestampCertificateWithoutExclusiveTimestampingEkuOnValidation()
    {
        using var fixture = SigningFixture.Create();
        using var timestampAuthority = TestTimestampAuthority.Create(
            fixture.SigningTime.AddSeconds(5),
            includeTimestampingEku: false);

        var result = await new PdfPadesSigningService().SignAsync(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions
            {
                Profile = PdfPadesProfile.BaselineT,
                SigningTime = fixture.SigningTime,
                Validation = new PdfPostSignValidationPolicy
                {
                    RequireRequestedPadesProfile = false
                }
            },
            timestampAuthority);

        await using var stream = new MemoryStream(result.Bytes);
        var signature = Assert.Single((await new PdfTrustInspector().InspectAsync(stream)).Signatures);
        Assert.Equal(PdfTrustStatus.Invalid, signature.TimestampStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.IntegrityStatus);
    }

    private static byte[] CreatePdf()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("PXA PAdES fixture", 40, 40);
        return document.ToBytes();
    }

    private static void AssertTimestampClose(DateTimeOffset expected, DateTimeOffset? actual)
    {
        Assert.NotNull(actual);
        Assert.InRange((actual.Value - expected).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
    }

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) ParseGraph(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }

    private static byte[] RewriteByteRange(byte[] source, Func<long[], long[]> mutation)
    {
        var result = source.ToArray();
        var markerOffset = result.AsSpan().IndexOf("/ByteRange ["u8);
        Assert.True(markerOffset >= 0);
        var valuesOffset = markerOffset + "/ByteRange ["u8.Length;
        var valuesEnd = result.AsSpan(valuesOffset).IndexOf((byte)']');
        Assert.True(valuesEnd > 0);
        var originalText = Encoding.ASCII.GetString(result, valuesOffset, valuesEnd);
        var tokens = originalText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, tokens.Length);
        var values = tokens.Select(long.Parse).ToArray();
        var replacements = mutation(values);
        Assert.Equal(4, replacements.Length);
        var replacementText = string.Join(' ', replacements.Select((value, index) =>
            value.ToString($"D{tokens[index].Length}", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(originalText.Length, replacementText.Length);
        Encoding.ASCII.GetBytes(replacementText, result.AsSpan(valuesOffset, valuesEnd));
        return result;
    }

    private static ParsedSignature ParseSignature(byte[] bytes, int objectNumber, int generation)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var signatureId = new PdfObjectId(objectNumber, generation);
        var dictionary = Assert.IsType<PdfDictionary>(graph.Resolve(signatureId)?.Value);
        var contents = Assert.IsType<PdfString>(dictionary["Contents"]).GetDecodedBytes().ToArray();
        var catalog = Assert.IsType<PdfDictionary>(Resolve(graph, graph.Trailer?["Root"]));
        var acroForm = Assert.IsType<PdfDictionary>(Resolve(graph, catalog["AcroForm"]));
        var fields = Assert.IsType<PdfArray>(Resolve(graph, acroForm["Fields"]));
        var referencesSignature = fields.Items
            .Select(field => Resolve(graph, field))
            .OfType<PdfDictionary>()
            .Any(field => field["V"] is PdfReference reference && reference.Id == signatureId);
        return new ParsedSignature(contents, referencesSignature);
    }

    private static ParsedSignature ParseSignature(byte[] bytes, string signedFieldName)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var signature = graph.Objects.Values
            .Select(static item => item.Value)
            .OfType<PdfDictionary>()
            .Single(dictionary => dictionary["Type"] is PdfName { Value: "Sig" });
        var contents = Assert.IsType<PdfString>(signature["Contents"]).GetDecodedBytes().ToArray();
        var catalog = Assert.IsType<PdfDictionary>(Resolve(graph, graph.Trailer?["Root"]));
        var acroForm = Assert.IsType<PdfDictionary>(Resolve(graph, catalog["AcroForm"]));
        var fields = Assert.IsType<PdfArray>(Resolve(graph, acroForm["Fields"]));
        var namedFieldExists = fields.Items
            .Select(field => Resolve(graph, field))
            .OfType<PdfDictionary>()
            .Any(field => Assert.IsType<PdfString>(field["T"]).ToLatin1String() == signedFieldName);
        return new ParsedSignature(contents, namedFieldExists);
    }

    private static ParsedWidget ParseWidget(byte[] bytes, string fieldName)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var catalog = Assert.IsType<PdfDictionary>(Resolve(graph, graph.Trailer?["Root"]));
        var acroForm = Assert.IsType<PdfDictionary>(Resolve(graph, catalog["AcroForm"]));
        var fields = Assert.IsType<PdfArray>(Resolve(graph, acroForm["Fields"]));
        var widgets = fields.Items
            .Select(field => Resolve(graph, field))
            .OfType<PdfDictionary>();
        var widget = Assert.Single(
            widgets,
            field => field["T"] is PdfString name && DecodePdfString(name) == fieldName);
        var rectangle = Assert.IsType<PdfArray>(widget["Rect"])
            .Items
            .Select(item => item switch
            {
                PdfNumber number => number.Value,
                PdfInteger integer => integer.Value,
                _ => double.NaN
            })
            .ToArray();
        var page = Assert.IsType<PdfDictionary>(Resolve(graph, widget["P"]));
        var appearance = Assert.IsType<PdfDictionary>(widget["AP"]);
        var appearanceStream = Assert.IsType<PdfStreamObject>(Resolve(graph, appearance["N"]));
        return new ParsedWidget(
            rectangle,
            Assert.IsType<PdfName>(page["Type"]).Value,
            Encoding.ASCII.GetString(appearanceStream.EncodedBytes.Span));
    }

    private static string DecodePdfString(PdfString value)
    {
        var content = value.GetDecodedBytes().Span;
        return content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF
            ? Encoding.BigEndianUnicode.GetString(content[2..])
            : Encoding.Latin1.GetString(content);
    }

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    }

    private static byte[] HashByteRanges(byte[] bytes, IReadOnlyList<long> byteRange)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(bytes.AsSpan(checked((int)byteRange[0]), checked((int)byteRange[1])));
        hash.AppendData(bytes.AsSpan(checked((int)byteRange[2]), checked((int)byteRange[3])));
        return hash.GetHashAndReset();
    }

    private static byte[] ReadByteRanges(byte[] bytes, IReadOnlyList<long> byteRange)
    {
        var first = bytes.AsSpan(checked((int)byteRange[0]), checked((int)byteRange[1]));
        var second = bytes.AsSpan(checked((int)byteRange[2]), checked((int)byteRange[3]));
        var result = new byte[first.Length + second.Length];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        return result;
    }

    private static CmsData ReadCms(ReadOnlySpan<byte> cms)
    {
        var contentInfo = new AsnReader(cms.ToArray(), AsnEncodingRules.DER).ReadSequence();
        Assert.Equal("1.2.840.113549.1.7.2", contentInfo.ReadObjectIdentifier());
        var explicitContent = contentInfo.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true));
        var signedData = explicitContent.ReadSequence();
        signedData.ReadInteger();
        signedData.ReadEncodedValue();
        signedData.ReadEncodedValue();
        signedData.ReadEncodedValue();
        var signerInfos = signedData.ReadSetOf();
        var signerInfo = signerInfos.ReadSequence();
        signerInfo.ReadInteger();
        signerInfo.ReadEncodedValue();
        signerInfo.ReadEncodedValue();
        var implicitAttributes = signerInfo.ReadEncodedValue().ToArray();
        implicitAttributes[0] = 0x31;
        signerInfo.ReadEncodedValue();
        var signature = signerInfo.ReadOctetString();

        byte[]? messageDigest = null;
        var attributes = new AsnReader(implicitAttributes, AsnEncodingRules.DER).ReadSetOf();
        while (attributes.HasData)
        {
            var attribute = attributes.ReadSequence();
            var oid = attribute.ReadObjectIdentifier();
            var values = attribute.ReadSetOf();
            if (oid == "1.2.840.113549.1.9.4")
            {
                messageDigest = values.ReadOctetString();
            }
        }

        return new CmsData(
            implicitAttributes,
            signature,
            Assert.IsType<byte[]>(messageDigest));
    }

    private static byte[] ReadSigningCertificateV2Hash(ReadOnlySpan<byte> signedAttributes)
    {
        var attributes = new AsnReader(signedAttributes.ToArray(), AsnEncodingRules.DER).ReadSetOf();
        while (attributes.HasData)
        {
            var attribute = attributes.ReadSequence();
            var oid = attribute.ReadObjectIdentifier();
            var values = attribute.ReadSetOf();
            if (oid != "1.2.840.113549.1.9.16.2.47")
            {
                continue;
            }

            var signingCertificate = values.ReadSequence();
            var certificateIds = signingCertificate.ReadSequence();
            var certificateId = certificateIds.ReadSequence();
            return certificateId.ReadOctetString();
        }

        throw new Xunit.Sdk.XunitException("The CAdES SigningCertificateV2 attribute is missing.");
    }

    private static int ReadDerLength(ReadOnlySpan<byte> value)
    {
        Assert.True(value.Length >= 2 && value[0] == 0x30);
        if ((value[1] & 0x80) == 0)
        {
            return 2 + value[1];
        }

        var lengthBytes = value[1] & 0x7F;
        Assert.InRange(lengthBytes, 1, 4);
        var contentLength = 0;
        for (var index = 0; index < lengthBytes; index++)
        {
            contentLength = (contentLength << 8) | value[2 + index];
        }

        return 2 + lengthBytes + contentLength;
    }

    private sealed record ParsedSignature(byte[] Contents, bool IsReferencedByAcroForm);

    private sealed record ParsedWidget(double[] Rectangle, string PageType, string AppearanceContent);

    private sealed record CmsData(byte[] SignedAttributes, byte[] Signature, byte[] MessageDigest);

    private sealed class SigningFixture : IDisposable
    {
        private SigningFixture(PdfPkcs12SigningKey key, DateTimeOffset signingTime)
        {
            Key = key;
            SigningTime = signingTime;
        }

        public PdfPkcs12SigningKey Key { get; }
        public DateTimeOffset SigningTime { get; }

        public static SigningFixture Create()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=PXA PDF Signing Test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
                critical: true));
            var now = DateTimeOffset.UtcNow;
            using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
            var pfx = certificate.Export(X509ContentType.Pkcs12, "test-password");
            return new SigningFixture(new PdfPkcs12SigningKey(pfx, "test-password"), now);
        }

        public void Dispose() => Key.Dispose();
    }

    private sealed class TestEvidenceProvider(PdfRevocationEvidence evidence) : IPdfRevocationEvidenceProvider
    {
        public int CallCount { get; private set; }

        public Task<PdfRevocationEvidenceResult> GetEvidenceAsync(
            PdfRevocationEvidenceRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new PdfRevocationEvidenceResult(evidence.Status, [evidence]));
        }
    }

    private sealed class TestIssuerCertificateProvider(byte[] certificate) : IPdfIssuerCertificateProvider
    {
        public int CallCount { get; private set; }

        public Task<PdfIssuerCertificateResult> GetIssuerCertificatesAsync(
            PdfIssuerCertificateRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new PdfIssuerCertificateResult([certificate]));
        }
    }

    private sealed class TestTimestampAuthority : IPdfTimestampAuthority, IDisposable
    {
        private const string TimestampInfoOid = "1.2.840.113549.1.9.16.1.4";
        private const string TimestampingExtendedKeyUsageOid = "1.3.6.1.5.5.7.3.8";
        private readonly X509Certificate2 _certificate;

        private TestTimestampAuthority(X509Certificate2 certificate, DateTimeOffset timestamp)
        {
            _certificate = certificate;
            Timestamp = timestamp;
        }

        public DateTimeOffset Timestamp { get; }
        public byte[] CertificateBytes => _certificate.RawData;
        public X509Certificate2 Certificate => _certificate;

        public static TestTimestampAuthority Create(
            DateTimeOffset timestamp,
            bool includeTimestampingEku = true)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=PXA Test TSA",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));
            if (includeTimestampingEku)
            {
                var usages = new OidCollection { new(TimestampingExtendedKeyUsageOid) };
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, critical: true));
            }
            return new TestTimestampAuthority(
                request.CreateSelfSigned(timestamp.AddDays(-1), timestamp.AddDays(1)),
                timestamp);
        }

        public Task<PdfTimestampToken> GetTimestampTokenAsync(
            ReadOnlyMemory<byte> signatureDigest,
            HashAlgorithmName digestAlgorithm,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(HashAlgorithmName.SHA256, digestAlgorithm);
            var tokenInfo = new Rfc3161TimestampTokenInfo(
                new Oid("1.2.3.4.5"),
                new Oid("2.16.840.1.101.3.4.2.1"),
                signatureDigest,
                new byte[] { 1 },
                Timestamp,
                accuracyInMicroseconds: null,
                isOrdering: false,
                nonce: null,
                timestampAuthorityName: null,
                extensions: null);
            var cms = new SignedCms(
                new ContentInfo(new Oid(TimestampInfoOid), tokenInfo.Encode()),
                detached: false);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _certificate)
            {
                IncludeOption = X509IncludeOption.EndCertOnly,
                DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1")
            };
            signer.SignedAttributes.Add(new AsnEncodedData(
                new Oid("1.2.840.113549.1.9.16.2.47"),
                BuildSigningCertificateV2(_certificate)));
            cms.ComputeSignature(signer);
            return Task.FromResult(new PdfTimestampToken(
                cms.Encode(),
                Timestamp,
                _certificate.Subject,
                _certificate.Thumbprint));
        }

        public void Dispose() => _certificate.Dispose();

        private static byte[] BuildSigningCertificateV2(X509Certificate2 certificate)
        {
            var writer = new AsnWriter(AsnEncodingRules.DER);
            writer.PushSequence();
            writer.PushSequence();
            writer.PushSequence();
            writer.WriteOctetString(SHA256.HashData(certificate.RawData));
            writer.PopSequence();
            writer.PopSequence();
            writer.PopSequence();
            return writer.Encode();
        }
    }
}
