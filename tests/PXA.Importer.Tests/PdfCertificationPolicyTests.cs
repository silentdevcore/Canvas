using System.Security.Cryptography;
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

public sealed class PdfCertificationPolicyTests
{
    [Fact]
    public async Task Certify_ShouldReportDocMdpLevelForAnUnchangedDocument()
    {
        using var fixture = CertificationFixture.Create();
        var certified = Certify(fixture, PdfCertificationLevel.FormFillingAndSignatures);

        var signature = Assert.Single(await InspectAsync(certified));

        Assert.True(signature.IsCertification);
        Assert.Equal(PdfTrustStatus.Valid, signature.CertificationStatus);
        Assert.NotNull(signature.Certification);
        Assert.Equal(2, signature.Certification.Level);
        Assert.Equal(0, signature.Certification.DisallowedChangeCount);
        Assert.Empty(signature.Certification.DisallowedChanges);
        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
    }

    [Fact]
    public async Task Certify_ShouldRejectContentChangesAfterTheCertifiedRevision()
    {
        using var fixture = CertificationFixture.Create();
        var certified = Certify(fixture, PdfCertificationLevel.FormFillingAndSignatures);
        var tampered = Append(certified, graph => [
            new PdfIncrementalObjectUpdate(
                FindContentStreamId(graph),
                new PdfStreamObject(new PdfDictionary(), "BT /F1 12 Tf (tampered) Tj ET"u8.ToArray()))
        ]);

        var report = await InspectReportAsync(tampered);
        var signature = Assert.Single(report.Signatures);

        Assert.Equal(PdfTrustStatus.Invalid, signature.CertificationStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.Status);
        Assert.Equal(PdfTrustStatus.Valid, signature.IntegrityStatus);
        Assert.Equal(1, signature.Certification!.DisallowedChangeCount);
        Assert.Contains("modified", Assert.Single(signature.Certification.DisallowedChanges));
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST147");
    }

    [Fact]
    public async Task Certify_ShouldAllowAnAdditionalApprovalSignatureAtLevelTwo()
    {
        using var fixture = CertificationFixture.Create();
        var certified = Certify(fixture, PdfCertificationLevel.FormFillingAndSignatures);
        var countersigned = new PdfPadesSigningService().Sign(
            certified,
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime }).Bytes;

        var signatures = await InspectAsync(countersigned);
        var certification = Assert.Single(signatures, static signature => signature.IsCertification);

        Assert.Equal(2, signatures.Count);
        Assert.Equal(PdfTrustStatus.Valid, certification.CertificationStatus);
        Assert.Equal(0, certification.Certification!.DisallowedChangeCount);
    }

    [Fact]
    public async Task Certify_ShouldRejectAnAdditionalSignatureWhenNoChangesAreAllowed()
    {
        using var fixture = CertificationFixture.Create();
        var certified = Certify(fixture, PdfCertificationLevel.NoChangesAllowed);
        var countersigned = new PdfPadesSigningService().Sign(
            certified,
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime }).Bytes;

        var certification = Assert.Single(
            await InspectAsync(countersigned),
            static signature => signature.IsCertification);

        Assert.Equal(1, certification.Certification!.Level);
        Assert.Equal(PdfTrustStatus.Invalid, certification.CertificationStatus);
        Assert.Contains(
            certification.Certification.DisallowedChanges,
            reason => reason.Contains("no changes allowed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(PdfCertificationLevel.FormFillingAndSignatures, PdfTrustStatus.Invalid)]
    [InlineData(PdfCertificationLevel.FormFillingAnnotationsAndSignatures, PdfTrustStatus.Valid)]
    public async Task Certify_ShouldOnlyPermitAddedAnnotationsAtLevelThree(
        PdfCertificationLevel level,
        PdfTrustStatus expected)
    {
        using var fixture = CertificationFixture.Create();
        var certified = Certify(fixture, level);
        var annotated = Append(certified, graph =>
        {
            var pageId = FindPageId(graph);
            var page = (PdfDictionary)graph.Resolve(pageId)!.Value;
            var annotationId = new PdfObjectId(graph.Objects.Keys.Max(static id => id.Number) + 1, 0);
            var annotations = new PdfArray(
                Resolve(graph, page["Annots"]) is PdfArray existing ? existing.Items : []);
            annotations.Add(new PdfReference(annotationId));
            var updatedPage = new PdfDictionary(page.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value)) { ["Annots"] = annotations };
            return [
                new PdfIncrementalObjectUpdate(annotationId, new PdfDictionary
                {
                    ["Type"] = new PdfName("Annot"),
                    ["Subtype"] = new PdfName("Text"),
                    ["Rect"] = new PdfArray([new PdfInteger(10), new PdfInteger(10), new PdfInteger(30), new PdfInteger(30)]),
                    ["Contents"] = new PdfString("Reviewed"u8.ToArray(), IsHex: false)
                }),
                new PdfIncrementalObjectUpdate(pageId, updatedPage)
            ];
        });

        var certification = Assert.Single(
            await InspectAsync(annotated),
            static signature => signature.IsCertification);

        Assert.Equal(expected, certification.CertificationStatus);
    }

    [Fact]
    public async Task Certify_ShouldRejectPageChangesOtherThanAnnotations()
    {
        using var fixture = CertificationFixture.Create();
        var certified = Certify(fixture, PdfCertificationLevel.FormFillingAnnotationsAndSignatures);
        var resized = Append(certified, graph =>
        {
            var pageId = FindPageId(graph);
            var page = (PdfDictionary)graph.Resolve(pageId)!.Value;
            var updatedPage = new PdfDictionary(page.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value))
            {
                ["MediaBox"] = new PdfArray([
                    new PdfInteger(0), new PdfInteger(0), new PdfInteger(200), new PdfInteger(200)
                ])
            };
            return [new PdfIncrementalObjectUpdate(pageId, updatedPage)];
        });

        var certification = Assert.Single(
            await InspectAsync(resized),
            static signature => signature.IsCertification);

        Assert.Equal(PdfTrustStatus.Invalid, certification.CertificationStatus);
        Assert.Contains(
            certification.Certification!.DisallowedChanges,
            reason => reason.Contains("/MediaBox", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Certify_ShouldRejectAValueChangeOnALockedField()
    {
        using var fixture = CertificationFixture.Create();
        var withForm = AddTextField(CreatePdf(), "Amount", "100");
        var certified = new PdfPadesSigningService().Sign(
            withForm,
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "Approval",
                SigningTime = fixture.SigningTime,
                Certification = PdfCertificationLevel.FormFillingAndSignatures,
                FieldLock = new PdfSignatureFieldLock
                {
                    Action = PdfSignatureFieldLockAction.Include,
                    Fields = ["Amount"]
                }
            }).Bytes;
        var edited = Append(certified, graph =>
        {
            var (fieldId, field) = FindField(graph, "Amount");
            var updated = new PdfDictionary(field.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value))
            {
                ["V"] = new PdfString("999"u8.ToArray(), IsHex: false)
            };
            return [new PdfIncrementalObjectUpdate(fieldId, updated)];
        });

        var certification = Assert.Single(
            await InspectAsync(edited),
            static signature => signature.IsCertification);

        Assert.Equal(["Amount"], certification.Certification!.LockedFields);
        Assert.Equal(PdfTrustStatus.Invalid, certification.Certification.FieldLockStatus);
        Assert.Equal(PdfTrustStatus.Invalid, certification.Status);
        Assert.Contains(
            certification.Certification.DisallowedChanges,
            reason => reason.Contains("FieldMDP", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Certify_ShouldAllowFillingAFieldThatIsNotLocked()
    {
        using var fixture = CertificationFixture.Create();
        var withForm = AddTextField(CreatePdf(), "Amount", "100");
        var certified = new PdfPadesSigningService().Sign(
            withForm,
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "Approval",
                SigningTime = fixture.SigningTime,
                Certification = PdfCertificationLevel.FormFillingAndSignatures
            }).Bytes;
        var filled = Append(certified, graph =>
        {
            var (fieldId, field) = FindField(graph, "Amount");
            var updated = new PdfDictionary(field.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value))
            {
                ["V"] = new PdfString("250"u8.ToArray(), IsHex: false)
            };
            return [new PdfIncrementalObjectUpdate(fieldId, updated)];
        });

        var certification = Assert.Single(
            await InspectAsync(filled),
            static signature => signature.IsCertification);

        Assert.Equal(PdfTrustStatus.Valid, certification.CertificationStatus);
        Assert.Equal(PdfTrustStatus.NotApplicable, certification.Certification!.FieldLockStatus);
    }

    [Fact]
    public async Task Certify_ShouldRejectANewFormFieldThatIsNotASignatureField()
    {
        using var fixture = CertificationFixture.Create();
        var certified = Certify(fixture, PdfCertificationLevel.FormFillingAndSignatures);
        var extended = Append(certified, graph =>
        {
            var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
            var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
            var acroForm = (PdfDictionary)Resolve(graph, catalog["AcroForm"])!;
            var fieldId = new PdfObjectId(graph.Objects.Keys.Max(static id => id.Number) + 1, 0);
            var fields = new PdfArray(((PdfArray)Resolve(graph, acroForm["Fields"])!).Items);
            fields.Add(new PdfReference(fieldId));
            var updatedForm = new PdfDictionary(acroForm.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value)) { ["Fields"] = fields };
            var updatedCatalog = new PdfDictionary(catalog.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value)) { ["AcroForm"] = updatedForm };
            return [
                new PdfIncrementalObjectUpdate(fieldId, new PdfDictionary
                {
                    ["FT"] = new PdfName("Tx"),
                    ["T"] = new PdfString("Injected"u8.ToArray(), IsHex: false),
                    ["V"] = new PdfString("value"u8.ToArray(), IsHex: false)
                }),
                new PdfIncrementalObjectUpdate(rootId, updatedCatalog)
            ];
        });

        var certification = Assert.Single(
            await InspectAsync(extended),
            static signature => signature.IsCertification);

        Assert.Equal(PdfTrustStatus.Invalid, certification.CertificationStatus);
        Assert.Contains(
            certification.Certification!.DisallowedChanges,
            reason => reason.Contains("not a signature field", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Certify_ShouldLockEveryFieldThatAnExcludeLockDoesNotName()
    {
        using var fixture = CertificationFixture.Create();
        var withForm = AddTextField(CreatePdf(), "Amount", "100");
        var certified = new PdfPadesSigningService().Sign(
            withForm,
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "Approval",
                SigningTime = fixture.SigningTime,
                Certification = PdfCertificationLevel.FormFillingAndSignatures,
                FieldLock = new PdfSignatureFieldLock
                {
                    Action = PdfSignatureFieldLockAction.Exclude,
                    Fields = ["Unrelated"]
                }
            }).Bytes;
        var edited = Append(certified, graph =>
        {
            var (fieldId, field) = FindField(graph, "Amount");
            var updated = new PdfDictionary(field.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value))
            {
                ["V"] = new PdfString("999"u8.ToArray(), IsHex: false)
            };
            return [new PdfIncrementalObjectUpdate(fieldId, updated)];
        });

        var certification = Assert.Single(
            await InspectAsync(edited),
            static signature => signature.IsCertification);

        Assert.Equal(PdfTrustStatus.Invalid, certification.Certification!.FieldLockStatus);
        Assert.Contains(
            certification.Certification.DisallowedChanges,
            reason => reason.Contains("'Amount'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Inspect_ShouldRejectPermsThatDoNotReferenceTheCertificationSignature()
    {
        using var fixture = CertificationFixture.Create();
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime }).Bytes;
        var forged = Append(signed, graph =>
        {
            var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
            var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
            var updated = new PdfDictionary(catalog.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value))
            {
                ["Perms"] = new PdfDictionary
                {
                    ["DocMDP"] = new PdfReference(FindSignatureId(graph))
                }
            };
            return [new PdfIncrementalObjectUpdate(rootId, updated)];
        });

        var report = await InspectReportAsync(forged);

        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST140");
        Assert.DoesNotContain(report.Signatures, static signature => signature.IsCertification);
    }

    [Fact]
    public async Task Certify_ShouldInvalidateAMismatchedCatalogPermsBinding()
    {
        using var fixture = CertificationFixture.Create();
        var certified = Certify(fixture, PdfCertificationLevel.FormFillingAndSignatures);
        var mismatched = Append(certified, graph =>
        {
            var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
            var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
            var updated = new PdfDictionary(catalog.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value))
            {
                ["Perms"] = new PdfDictionary { ["DocMDP"] = new PdfReference(rootId) }
            };
            return [new PdfIncrementalObjectUpdate(rootId, updated)];
        });

        var report = await InspectReportAsync(mismatched);
        var signature = Assert.Single(report.Signatures, static candidate => candidate.IsCertification);

        Assert.Equal(PdfTrustStatus.Invalid, signature.CertificationStatus);
        Assert.Equal(PdfTrustStatus.Invalid, signature.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST142");
        Assert.Contains(
            signature.Certification!.DisallowedChanges,
            reason => reason.Contains("/Perms /DocMDP", StringComparison.Ordinal));
    }

    [Fact]
    public void Sign_ShouldRejectCertificationWhenTheDocumentIsAlreadySigned()
    {
        using var fixture = CertificationFixture.Create();
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime }).Bytes;

        var exception = Assert.Throws<InvalidDataException>(() => new PdfPadesSigningService().Sign(
            signed,
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "Certification",
                SigningTime = fixture.SigningTime,
                Certification = PdfCertificationLevel.NoChangesAllowed
            }));

        Assert.Contains("first signature", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Certify_ShouldNotTreatAnUncertifiedSignatureAsCertification()
    {
        using var fixture = CertificationFixture.Create();
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime }).Bytes;

        var signature = Assert.Single(await InspectAsync(signed));

        Assert.False(signature.IsCertification);
        Assert.Equal(PdfTrustStatus.NotApplicable, signature.CertificationStatus);
        Assert.Null(signature.Certification);
    }

    private static byte[] Certify(CertificationFixture fixture, PdfCertificationLevel level) =>
        new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions
            {
                FieldName = "Certification",
                SigningTime = fixture.SigningTime,
                Certification = level
            }).Bytes;

    private static byte[] CreatePdf()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("PXA certification fixture", 40, 40);
        return document.ToBytes();
    }

    private static byte[] AddTextField(byte[] source, string name, string value) =>
        Append(source, graph =>
        {
            var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
            var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
            var fieldId = new PdfObjectId(graph.Objects.Keys.Max(static id => id.Number) + 1, 0);
            var updatedCatalog = new PdfDictionary(catalog.Values.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value))
            {
                ["AcroForm"] = new PdfDictionary
                {
                    ["Fields"] = new PdfArray([new PdfReference(fieldId)])
                }
            };
            return [
                new PdfIncrementalObjectUpdate(fieldId, new PdfDictionary
                {
                    ["FT"] = new PdfName("Tx"),
                    ["T"] = new PdfString(Encoding.Latin1.GetBytes(name), IsHex: false),
                    ["V"] = new PdfString(Encoding.Latin1.GetBytes(value), IsHex: false)
                }),
                new PdfIncrementalObjectUpdate(rootId, updatedCatalog)
            ];
        });

    private static byte[] Append(
        byte[] source,
        Func<PdfObjectGraph, IReadOnlyList<PdfIncrementalObjectUpdate>> build) =>
        new PdfIncrementalUpdateWriter().Append(source, build(ParseGraph(source))).Bytes;

    private static PdfObjectGraph ParseGraph(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return new PdfObjectParser(context, table).ParseDocumentGraph();
    }

    private static async Task<IReadOnlyList<PdfSignatureInspection>> InspectAsync(byte[] bytes) =>
        (await InspectReportAsync(bytes)).Signatures;

    private static async Task<PdfTrustReport> InspectReportAsync(byte[] bytes)
    {
        await using var stream = new MemoryStream(bytes);
        return await new PdfTrustInspector().InspectAsync(stream);
    }

    private static PdfObjectId FindPageId(PdfObjectGraph graph) =>
        graph.Objects.Values
            .Where(static value => value.Value is PdfDictionary { } dictionary &&
                dictionary["Type"] is PdfName { Value: "Page" })
            .Select(static value => value.Id)
            .First();

    private static PdfObjectId FindSignatureId(PdfObjectGraph graph) =>
        graph.Objects.Values
            .Where(static value => value.Value is PdfDictionary dictionary &&
                dictionary["Type"] is PdfName { Value: "Sig" })
            .Select(static value => value.Id)
            .First();

    private static PdfObjectId FindContentStreamId(PdfObjectGraph graph)
    {
        var page = (PdfDictionary)graph.Resolve(FindPageId(graph))!.Value;
        return page["Contents"] is PdfReference reference
            ? reference.Id
            : throw new InvalidOperationException("The fixture page does not use an indirect content stream.");
    }

    private static (PdfObjectId Id, PdfDictionary Field) FindField(PdfObjectGraph graph, string name)
    {
        foreach (var indirectObject in graph.Objects.Values)
        {
            if (indirectObject.Value is PdfDictionary dictionary &&
                dictionary["FT"] is not null &&
                dictionary["T"] is PdfString title &&
                string.Equals(title.ToLatin1String(), name, StringComparison.Ordinal))
            {
                return (indirectObject.Id, dictionary);
            }
        }

        throw new InvalidOperationException($"The fixture does not contain a field named '{name}'.");
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

    private sealed class CertificationFixture : IDisposable
    {
        private CertificationFixture(PdfPkcs12SigningKey key, DateTimeOffset signingTime)
        {
            Key = key;
            SigningTime = signingTime;
        }

        public PdfPkcs12SigningKey Key { get; }
        public DateTimeOffset SigningTime { get; }

        public static CertificationFixture Create()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=PXA PDF Certification Test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
                critical: true));
            var now = DateTimeOffset.UtcNow;
            using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
            var pfx = certificate.Export(X509ContentType.Pkcs12, "test-password");
            return new CertificationFixture(new PdfPkcs12SigningKey(pfx, "test-password"), now);
        }

        public void Dispose() => Key.Dispose();
    }
}
