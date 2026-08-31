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

public sealed class PdfFullRewriteWriterTests
{
    [Fact]
    public void Rewrite_ShouldDecodeAndReencodeEscapedPdfNamesExactlyOnce()
    {
        var source = CreatePdf();
        var (_, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var markerId = new PdfObjectId(NextObjectNumber(source), 0);
        var updatedCatalog = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["PxaName"] = new PdfReference(markerId)
        };
        var appended = new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(markerId, new PdfDictionary
            {
                ["Subtype"] = new PdfName("text/plain"),
                ["Key With Space"] = new PdfName("value#fragment")
            }),
            new PdfIncrementalObjectUpdate(rootId, updatedCatalog)
        ]).Bytes;

        var rewritten = new PdfFullRewriteWriter().Rewrite(appended).Bytes;
        var (_, rewrittenGraph) = ParseGraph(rewritten);
        var marker = Assert.IsType<PdfDictionary>(rewrittenGraph.Objects.Values
            .Single(item => item.Value is PdfDictionary dictionary && dictionary["Subtype"] is PdfName { Value: "text/plain" })
            .Value);

        Assert.Equal("value#fragment", Assert.IsType<PdfName>(marker["Key With Space"]).Value);
        var serialized = Encoding.Latin1.GetString(rewritten);
        Assert.Contains("/Subtype /text#2Fplain", serialized, StringComparison.Ordinal);
        Assert.Contains("/Key#20With#20Space /value#23fragment", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("text#232Fplain", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Rewrite_ShouldProduceASingleRevisionThatStillParses()
    {
        var source = CreatePdf();

        var result = new PdfFullRewriteWriter().Rewrite(source);

        var (table, graph) = ParseGraph(result.Bytes);
        Assert.Single(table.Revisions);
        Assert.Empty(table.Diagnostics);
        Assert.Equal(result.ObjectCount, graph.Objects.Count);
        Assert.NotNull(graph.Trailer?["Root"]);
    }

    [Fact]
    public void Rewrite_ShouldBeDeterministic()
    {
        var source = CreatePdf();

        var first = new PdfFullRewriteWriter().Rewrite(source);
        var second = new PdfFullRewriteWriter().Rewrite(source);

        Assert.Equal(first.Bytes, second.Bytes);
    }

    [Fact]
    public void Rewrite_ShouldDropEarlierRevisions()
    {
        var source = CreatePdf();
        var (_, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var markerId = new PdfObjectId(NextObjectNumber(source), 0);
        var updatedCatalog = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["PxaMarker"] = new PdfReference(markerId)
        };
        var appended = new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(new PdfObjectId(NextObjectNumber(source), 0), new PdfDictionary
            {
                ["Type"] = new PdfName("PxaMarker"),
                ["Secret"] = new PdfString("must-not-survive"u8.ToArray(), IsHex: false)
            }),
            new PdfIncrementalObjectUpdate(rootId, updatedCatalog)
        ]).Bytes;
        Assert.Equal(2, ParseGraph(appended).Table.Revisions.Count);

        var result = new PdfFullRewriteWriter().Rewrite(appended);

        Assert.Single(ParseGraph(result.Bytes).Table.Revisions);
        // The marker object is still reachable, so it survives; the earlier revision's copy of
        // the file does not.
        Assert.Equal(1, CountOccurrences(result.Bytes, "%%EOF"u8));
        Assert.Equal(1, CountOccurrences(result.Bytes, "startxref"u8));
    }

    [Fact]
    public void Rewrite_ShouldRemoveContentThatOnlySurvivesInASupersededRevision()
    {
        // This is the property that makes a full rewrite the required strategy for redaction:
        // an incremental update leaves the superseded bytes in the file, a rewrite does not.
        var source = CreatePdf();
        var markerId = new PdfObjectId(NextObjectNumber(source), 0);
        var (_, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var updatedCatalog = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["PxaMarker"] = new PdfReference(markerId)
        };
        var withSecret = new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(markerId, new PdfStreamObject(
                new PdfDictionary { ["Type"] = new PdfName("PxaMarker") },
                "TOP-SECRET-PAYLOAD"u8.ToArray())),
            new PdfIncrementalObjectUpdate(rootId, updatedCatalog)
        ]).Bytes;
        var superseded = new PdfIncrementalUpdateWriter().Append(withSecret, [
            new PdfIncrementalObjectUpdate(markerId, new PdfStreamObject(
                new PdfDictionary { ["Type"] = new PdfName("PxaMarker") },
                "redacted"u8.ToArray()))
        ]).Bytes;
        Assert.Equal(1, CountOccurrences(superseded, "TOP-SECRET-PAYLOAD"u8));

        var result = new PdfFullRewriteWriter().Rewrite(superseded);

        Assert.Equal(0, CountOccurrences(result.Bytes, "TOP-SECRET-PAYLOAD"u8));
        Assert.Equal(1, CountOccurrences(result.Bytes, "redacted"u8));
    }

    [Fact]
    public void Rewrite_ShouldPreserveUnknownObjectsAndStreamPayloads()
    {
        var source = CreatePdf();
        var markerId = new PdfObjectId(NextObjectNumber(source), 0);
        var (_, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var updatedCatalog = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["PxaUnknown"] = new PdfReference(markerId)
        };
        var payload = "PXA unknown payload"u8.ToArray();
        var appended = new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(markerId, new PdfStreamObject(
                new PdfDictionary { ["Type"] = new PdfName("PxaUnknownType"), ["PxaCustom"] = new PdfInteger(7) },
                payload)),
            new PdfIncrementalObjectUpdate(rootId, updatedCatalog)
        ]).Bytes;

        var result = new PdfFullRewriteWriter().Rewrite(appended);

        var (_, rewrittenGraph) = ParseGraph(result.Bytes);
        var preserved = rewrittenGraph.Objects.Values
            .Select(static value => value.Value)
            .OfType<PdfStreamObject>()
            .Single(static value => value.Dictionary["Type"] is PdfName { Value: "PxaUnknownType" });
        Assert.Equal(7, Assert.IsType<PdfInteger>(preserved.Dictionary["PxaCustom"]).Value);
        Assert.Equal(payload, preserved.EncodedBytes.ToArray());
        Assert.Equal(payload.Length, Assert.IsType<PdfInteger>(preserved.Dictionary["Length"]).Value);
    }

    [Fact]
    public void Rewrite_ShouldDropUnreachableObjectsAndTheirPayloads()
    {
        var source = CreatePdf();
        var orphanId = new PdfObjectId(NextObjectNumber(source), 0);
        var appended = new PdfIncrementalUpdateWriter().Append(source, [
            new PdfIncrementalObjectUpdate(orphanId, new PdfStreamObject(
                new PdfDictionary { ["Type"] = new PdfName("PxaOrphan") },
                "ORPHANED-SECRET-PAYLOAD"u8.ToArray()))
        ]).Bytes;

        var result = new PdfFullRewriteWriter().Rewrite(appended);

        Assert.DoesNotContain("ORPHANED-SECRET-PAYLOAD", Encoding.Latin1.GetString(result.Bytes), StringComparison.Ordinal);
        Assert.Contains(orphanId, result.RemovedObjects);
    }

    [Fact]
    public void Rewrite_ShouldRenumberObjectsCompactlyAndKeepReferencesIntact()
    {
        var source = CreatePdf();

        var result = new PdfFullRewriteWriter().Rewrite(source);

        var (table, graph) = ParseGraph(result.Bytes);
        Assert.Equal(
            Enumerable.Range(1, result.ObjectCount).ToArray(),
            table.Entries.Keys.Where(id => id.Number > 0).Select(static id => id.Number).Order().ToArray());
        Assert.All(graph.Objects.Keys, id => Assert.Equal(0, id.Generation));

        var root = Assert.IsType<PdfReference>(graph.Trailer!["Root"]);
        var catalog = Assert.IsType<PdfDictionary>(graph.Resolve(root.Id)!.Value);
        var pages = Assert.IsType<PdfReference>(catalog["Pages"]);
        Assert.IsType<PdfDictionary>(graph.Resolve(pages.Id)!.Value);
    }

    [Fact]
    public void Rewrite_ShouldWriteADeterministicDocumentId()
    {
        var result = new PdfFullRewriteWriter().Rewrite(CreatePdf());

        var (_, graph) = ParseGraph(result.Bytes);
        var id = Assert.IsType<PdfArray>(graph.Trailer!["ID"]);
        Assert.Equal(2, id.Items.Count);
        var first = Assert.IsType<PdfString>(id.Items[0]);
        Assert.Equal(16, first.GetDecodedBytes().Length);
        Assert.Equal(first.GetDecodedBytes().ToArray(), Assert.IsType<PdfString>(id.Items[1]).GetDecodedBytes().ToArray());
    }

    [Fact]
    public void Rewrite_ShouldRefuseToSilentlyInvalidateSignatures()
    {
        using var fixture = RewriteSigningFixture.Create();
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime }).Bytes;

        var exception = Assert.Throws<InvalidOperationException>(
            () => new PdfFullRewriteWriter().Rewrite(signed));

        Assert.Contains("AllowSignatureInvalidation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rewrite_ShouldReportTheSignaturesItInvalidatesWhenAllowed()
    {
        using var fixture = RewriteSigningFixture.Create();
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime }).Bytes;

        var result = new PdfFullRewriteWriter().Rewrite(
            signed,
            new PdfFullRewriteOptions { AllowSignatureInvalidation = true });

        Assert.Single(result.InvalidatedSignatures);
        await using var stream = new MemoryStream(result.Bytes);
        var report = await new PdfTrustInspector().InspectAsync(stream);
        // The signature dictionary survives as content, but no longer covers a valid revision.
        var signature = Assert.Single(report.Signatures);
        Assert.NotEqual(PdfTrustStatus.Valid, signature.Status);
    }

    [Fact]
    public void Rewrite_ShouldRejectEncryptedDocuments()
    {
        var source = CreatePdf();
        var encrypted = Encoding.Latin1.GetString(source).Replace("/Root", "/Encrypt 1 0 R /Root", StringComparison.Ordinal);

        Assert.ThrowsAny<Exception>(
            () => new PdfFullRewriteWriter().Rewrite(Encoding.Latin1.GetBytes(encrypted)));
    }

    [Fact]
    public void Save_ShouldSelectTheRequestedStrategy()
    {
        var source = CreatePdf();

        var incremental = new PdfDocumentSaver().Save(source, new PdfSaveRequest
        {
            Strategy = PdfSaveStrategy.IncrementalUpdate,
            Updates = [
                new PdfIncrementalObjectUpdate(
                    new PdfObjectId(NextObjectNumber(source), 0),
                    new PdfDictionary { ["Type"] = new PdfName("PxaMarker") })
            ]
        });
        var rewrite = new PdfDocumentSaver().Save(source, new PdfSaveRequest
        {
            Strategy = PdfSaveStrategy.FullRewrite
        });

        Assert.Equal(2, incremental.RevisionCount);
        Assert.Single(incremental.ChangedObjects);
        Assert.Equal(1, rewrite.RevisionCount);
        Assert.Empty(rewrite.InvalidatedSignatures);
    }

    [Fact]
    public void Save_ShouldRejectAnIncrementalUpdateWithNoObjects() =>
        Assert.Throws<ArgumentException>(() => new PdfDocumentSaver().Save(CreatePdf(), new PdfSaveRequest
        {
            Strategy = PdfSaveStrategy.IncrementalUpdate
        }));

    [Fact]
    public void Rewrite_ShouldRejectAnEmptySource() =>
        Assert.Throws<InvalidDataException>(() => new PdfFullRewriteWriter().Rewrite(ReadOnlyMemory<byte>.Empty));

    private static byte[] CreatePdf()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("PXA full rewrite fixture", 40, 40);
        return document.ToBytes();
    }

    private static int NextObjectNumber(byte[] source) =>
        ParseGraph(source).Table.Entries.Keys.Max(static id => id.Number) + 1;

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) ParseGraph(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }

    private sealed class RewriteSigningFixture : IDisposable
    {
        private RewriteSigningFixture(PdfPkcs12SigningKey key, DateTimeOffset signingTime)
        {
            Key = key;
            SigningTime = signingTime;
        }

        public PdfPkcs12SigningKey Key { get; }
        public DateTimeOffset SigningTime { get; }

        public static RewriteSigningFixture Create()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=PXA PDF Rewrite Test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
                critical: true));
            var now = DateTimeOffset.UtcNow;
            using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
            return new RewriteSigningFixture(
                new PdfPkcs12SigningKey(certificate.Export(X509ContentType.Pkcs12, "test-password"), "test-password"),
                now);
        }

        public void Dispose() => Key.Dispose();
    }

    private static int CountOccurrences(byte[] source, ReadOnlySpan<byte> marker)
    {
        var count = 0;
        var offset = 0;
        while (offset < source.Length)
        {
            var found = source.AsSpan(offset).IndexOf(marker);
            if (found < 0)
            {
                break;
            }

            count++;
            offset += found + marker.Length;
        }

        return count;
    }
}
