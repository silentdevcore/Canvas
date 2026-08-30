using System.Security.Cryptography;
using System.Text;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Security;
using PXA.Importer.Signing;
using PXA.Importer.Xref;
using PXA.Pdf;

namespace PXA.Importer.Tests;

public sealed class PdfEncryptedDocumentTests
{
    private const string Secret = "PXA-ENCRYPTION-PROBE-71923";

    [Fact]
    public void Encrypt_ShouldRemoveThePlaintextFromTheFileBytes()
    {
        var source = CreatePdf();
        Assert.Contains(Secret, Encoding.Latin1.GetString(source), StringComparison.Ordinal);

        var encrypted = Encrypt(source, "open-me");

        Assert.DoesNotContain(Secret, Encoding.Latin1.GetString(encrypted), StringComparison.Ordinal);
    }

    [Fact]
    public void Encrypt_ShouldDeclareRevision6InTheTrailer()
    {
        var (table, graph) = ParseGraph(Encrypt(CreatePdf(), "open-me"));

        Assert.True(PdfDocumentDecryptor.IsEncrypted(table));
        var encrypt = Assert.IsType<PdfDictionary>(
            graph.Resolve(((PdfReference)table.Trailer!["Encrypt"]!).Id)!.Value);
        Assert.Equal(5, Assert.IsType<PdfInteger>(encrypt["V"]).Value);
        Assert.Equal(6, Assert.IsType<PdfInteger>(encrypt["R"]).Value);
        Assert.Equal("2.0", ReadHeaderVersion(Encrypt(CreatePdf(), "open-me")));
    }

    [Fact]
    public void Decrypt_ShouldRecoverTheOriginalContentWithTheUserPassword()
    {
        var encrypted = Encrypt(CreatePdf(), "open-me", "owner-key");
        var (table, graph) = ParseGraph(encrypted);

        var result = PdfDocumentDecryptor.Decrypt(table, graph, "open-me");

        Assert.Equal(PdfPasswordKind.User, result.PasswordKind);
        Assert.True(result.PermissionsVerified);
        Assert.Contains(Secret, ExtractStreamText(graph), StringComparison.Ordinal);
    }

    [Fact]
    public void Decrypt_ShouldRecoverTheOriginalContentWithTheOwnerPassword()
    {
        var encrypted = Encrypt(CreatePdf(), "open-me", "owner-key");
        var (table, graph) = ParseGraph(encrypted);

        var result = PdfDocumentDecryptor.Decrypt(table, graph, "owner-key");

        Assert.Equal(PdfPasswordKind.Owner, result.PasswordKind);
        Assert.Contains(Secret, ExtractStreamText(graph), StringComparison.Ordinal);
    }

    [Fact]
    public void Decrypt_ShouldRejectAWrongPassword()
    {
        var (table, graph) = ParseGraph(Encrypt(CreatePdf(), "open-me", "owner-key"));

        Assert.Throws<PdfPasswordException>(() => PdfDocumentDecryptor.Decrypt(table, graph, "guess"));
    }

    [Fact]
    public void Decrypt_ShouldRecoverDocumentStringsNotOnlyStreams()
    {
        var source = AddCatalogString(CreatePdf(), "PXA-STRING-MARKER");
        var encrypted = Encrypt(source, "open-me");
        Assert.DoesNotContain("PXA-STRING-MARKER", Encoding.Latin1.GetString(encrypted), StringComparison.Ordinal);
        var (table, graph) = ParseGraph(encrypted);

        PdfDocumentDecryptor.Decrypt(table, graph, "open-me");

        var catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
        Assert.Equal("PXA-STRING-MARKER", Assert.IsType<PdfString>(catalog["PxaMarker"]).ToLatin1String());
    }

    [Fact]
    public void Encrypt_ShouldProduceDifferentBytesEachRunBecauseSaltsAreRandom()
    {
        var source = CreatePdf();

        Assert.NotEqual(Encrypt(source, "open-me"), Encrypt(source, "open-me"));
    }

    [Fact]
    public void Decrypt_ShouldCarryThroughTheGrantedPermissions()
    {
        var encrypted = Encrypt(
            CreatePdf(),
            "open-me",
            "owner-key",
            PdfDocumentPermissions.Print | PdfDocumentPermissions.ExtractForAccessibility);
        var (table, graph) = ParseGraph(encrypted);

        var result = PdfDocumentDecryptor.Decrypt(table, graph, "open-me");

        Assert.Equal(
            PdfDocumentPermissions.Print | PdfDocumentPermissions.ExtractForAccessibility,
            result.Permissions);
        Assert.True(result.PermissionsVerified);
    }

    [Fact]
    public void Encrypt_ShouldRefuseToEncryptASignedDocument()
    {
        using var fixture = EncryptionSigningFixture.Create();
        var signed = new PdfPadesSigningService().Sign(
            CreatePdf(),
            fixture.Key,
            new PdfSigningOptions { FieldName = "Approval", SigningTime = fixture.SigningTime }).Bytes;

        var exception = Assert.Throws<NotSupportedException>(() => Encrypt(signed, "open-me"));

        Assert.Contains("exemption is not implemented", exception.Message, StringComparison.Ordinal);

        // Accepting signature invalidation does not unlock it: the exemption is a missing
        // capability, not a consequence the caller can agree to.
        var forced = Assert.Throws<NotSupportedException>(() => new PdfFullRewriteWriter().Rewrite(
            signed,
            new PdfFullRewriteOptions
            {
                AllowSignatureInvalidation = true,
                Encryption = new PdfDocumentEncryptionOptions { UserPassword = "open-me" }
            }));
        Assert.Contains("exemption is not implemented", forced.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Encrypt_ShouldStillProduceASingleRevision()
    {
        var (table, _) = ParseGraph(Encrypt(CreatePdf(), "open-me"));

        Assert.Single(table.Revisions);
        Assert.Empty(table.Diagnostics);
    }

    [Fact]
    public void ParseAndDecrypt_ShouldDecryptObjectStreamsBeforeExpandingCompressedObjects()
    {
        var encrypted = CreateEncryptedObjectStreamPdf("open-me");
        var context = new PdfParseContext(encrypted, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);

        var (graph, result) = PdfDocumentDecryptor.ParseAndDecrypt(context, table, "open-me");

        Assert.Equal(PdfPasswordKind.Owner, result.PasswordKind);
        var compressed = Assert.IsType<PdfDictionary>(graph.Resolve(new PdfObjectId(5, 0))!.Value);
        Assert.Equal(
            "OBJECT-STREAM-SECRET",
            Assert.IsType<PdfString>(compressed["Marker"]).ToLatin1String());
    }

    [Fact]
    public async Task ImportFacade_ShouldRequireAndUseThePasswordWithoutStoringItInOptions()
    {
        var encrypted = Encrypt(CreatePdf(), "open-me", "owner-secret");

        await Assert.ThrowsAsync<PdfPasswordException>(async () =>
            await Pdf.LoadAsync(new MemoryStream(encrypted)));
        var document = await Pdf.LoadEncryptedAsync(
            new MemoryStream(encrypted),
            "open-me");

        Assert.Single(document.Pages);
        Assert.NotEmpty(document.Pages[0].ContentStreams);
    }

    [Fact]
    public void Decrypt_ShouldNotPartiallyMutateTheGraphWhenOneValueIsInvalid()
    {
        var encrypted = Encrypt(AddCatalogString(CreatePdf(), "CATALOG-SECRET"), "open-me");
        var (table, graph) = ParseGraph(encrypted);
        var catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
        var originalCatalogCiphertext = Assert.IsType<PdfString>(catalog["PxaMarker"])
            .GetDecodedBytes()
            .ToArray();
        var streamObject = graph.Objects.Values
            .First(static value => value.Value is PdfStreamObject);
        var stream = (PdfStreamObject)streamObject.Value;
        var corrupted = stream.EncodedBytes.ToArray();
        corrupted[^1] ^= 0xFF;
        streamObject.Value = new PdfStreamObject(stream.Dictionary, corrupted);

        Assert.Throws<CryptographicException>(
            () => PdfDocumentDecryptor.Decrypt(table, graph, "open-me"));

        catalog = (PdfDictionary)graph.Resolve(((PdfReference)graph.Trailer!["Root"]!).Id)!.Value;
        Assert.Equal(
            originalCatalogCiphertext,
            Assert.IsType<PdfString>(catalog["PxaMarker"]).GetDecodedBytes().ToArray());
    }

    private static byte[] Encrypt(
        byte[] source,
        string userPassword,
        string? ownerPassword = null,
        PdfDocumentPermissions permissions = PdfDocumentPermissions.All) =>
        new PdfFullRewriteWriter().Rewrite(source, new PdfFullRewriteOptions
        {
            Encryption = new PdfDocumentEncryptionOptions
            {
                UserPassword = userPassword,
                OwnerPassword = ownerPassword,
                Permissions = permissions
            }
        }).Bytes;

    private static byte[] CreatePdf()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText(Secret, 40, 40);
        return document.ToBytes();
    }

    private static byte[] AddCatalogString(byte[] source, string marker)
    {
        var (_, graph) = ParseGraph(source);
        var rootId = ((PdfReference)graph.Trailer!["Root"]!).Id;
        var catalog = (PdfDictionary)graph.Resolve(rootId)!.Value;
        var updated = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value))
        {
            ["PxaMarker"] = new PdfString(Encoding.Latin1.GetBytes(marker), IsHex: false)
        };
        return new PdfIncrementalUpdateWriter()
            .Append(source, [new PdfIncrementalObjectUpdate(rootId, updated)]).Bytes;
    }

    private static byte[] CreateEncryptedObjectStreamPdf(string password)
    {
        using var handler = PdfStandardSecurityHandler.Create(
            new PdfDocumentEncryptionOptions { UserPassword = password });
        using var body = new MemoryStream();
        PdfObjectSerializer.WriteAscii(body, "%PDF-2.0\n");
        body.Write([(byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);

        var offsets = new Dictionary<int, long>();
        void WriteObject(int number, PdfObject value)
        {
            offsets[number] = body.Position;
            PdfObjectSerializer.WriteIndirectObject(body, new PdfObjectId(number, 0), value);
        }

        WriteObject(1, new PdfDictionary
        {
            ["Type"] = new PdfName("Catalog"),
            ["Pages"] = new PdfReference(new PdfObjectId(4, 0)),
            ["CompressedProbe"] = new PdfReference(new PdfObjectId(5, 0))
        });
        WriteObject(2, handler.BuildEncryptDictionary());

        var objectStreamPlaintext = Encoding.ASCII.GetBytes(
            "5 0 << /Type /PxaCompressed /Marker (OBJECT-STREAM-SECRET) >>");
        WriteObject(3, new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("ObjStm"),
                ["N"] = new PdfInteger(1),
                ["First"] = new PdfInteger(4)
            },
            handler.EncryptValue(objectStreamPlaintext)));
        WriteObject(4, new PdfDictionary
        {
            ["Type"] = new PdfName("Pages"),
            ["Kids"] = new PdfArray([]),
            ["Count"] = new PdfInteger(0)
        });

        var xrefOffset = body.Position;
        offsets[6] = xrefOffset;
        var xrefBytes = new byte[7 * 7];
        WriteXrefEntry(xrefBytes, 0, type: 0, field1: 0, field2: 65535);
        WriteXrefEntry(xrefBytes, 1, type: 1, field1: offsets[1], field2: 0);
        WriteXrefEntry(xrefBytes, 2, type: 1, field1: offsets[2], field2: 0);
        WriteXrefEntry(xrefBytes, 3, type: 1, field1: offsets[3], field2: 0);
        WriteXrefEntry(xrefBytes, 4, type: 1, field1: offsets[4], field2: 0);
        WriteXrefEntry(xrefBytes, 5, type: 2, field1: 3, field2: 0);
        WriteXrefEntry(xrefBytes, 6, type: 1, field1: offsets[6], field2: 0);

        WriteObject(6, new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("XRef"),
                ["Size"] = new PdfInteger(7),
                ["W"] = new PdfArray([new PdfInteger(1), new PdfInteger(4), new PdfInteger(2)]),
                ["Root"] = new PdfReference(new PdfObjectId(1, 0)),
                ["Encrypt"] = new PdfReference(new PdfObjectId(2, 0)),
                ["ID"] = new PdfArray([
                    new PdfString(new byte[16], IsHex: false),
                    new PdfString(new byte[16], IsHex: false)
                ])
            },
            xrefBytes));
        PdfObjectSerializer.WriteAscii(body, $"startxref\n{xrefOffset}\n%%EOF\n");
        return body.ToArray();
    }

    private static void WriteXrefEntry(
        byte[] destination,
        int index,
        byte type,
        long field1,
        int field2)
    {
        var offset = index * 7;
        destination[offset] = type;
        destination[offset + 1] = (byte)(field1 >> 24);
        destination[offset + 2] = (byte)(field1 >> 16);
        destination[offset + 3] = (byte)(field1 >> 8);
        destination[offset + 4] = (byte)field1;
        destination[offset + 5] = (byte)(field2 >> 8);
        destination[offset + 6] = (byte)field2;
    }

    private static string ExtractStreamText(PdfObjectGraph graph) => string.Join(
        '\n',
        graph.Objects.Values
            .Select(static value => value.Value)
            .OfType<PdfStreamObject>()
            .Select(static value => Encoding.Latin1.GetString(value.EncodedBytes.Span)));

    private static string ReadHeaderVersion(byte[] pdf) =>
        Encoding.ASCII.GetString(pdf, 5, 3);

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph) ParseGraph(byte[] bytes)
    {
        var context = new PdfParseContext(bytes, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        return (table, new PdfObjectParser(context, table).ParseDocumentGraph());
    }

    private sealed class EncryptionSigningFixture : IDisposable
    {
        private EncryptionSigningFixture(PdfPkcs12SigningKey key, DateTimeOffset signingTime)
        {
            Key = key;
            SigningTime = signingTime;
        }

        public PdfPkcs12SigningKey Key { get; }
        public DateTimeOffset SigningTime { get; }

        public static EncryptionSigningFixture Create()
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                "CN=PXA PDF Encryption Test",
                rsa,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            var now = DateTimeOffset.UtcNow;
            using var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(2));
            return new EncryptionSigningFixture(
                new PdfPkcs12SigningKey(
                    certificate.Export(
                        System.Security.Cryptography.X509Certificates.X509ContentType.Pkcs12,
                        "test-password"),
                    "test-password"),
                now);
        }

        public void Dispose() => Key.Dispose();
    }
}
