using PVAD.Importer.Objects;
using PVAD.Importer.Trust;

namespace PVAD.Importer.Tests;

public sealed class PdfEmbeddedValidationMaterialTests
{
    [Fact]
    public void Extract_ShouldReadAndDeduplicateDssAndVriMaterial()
    {
        byte[] signatureValue = [0x30, 0x03, 0x01, 0x02, 0x03];
        var graph = CreateGraph(
            new PdfStreamObject(new PdfDictionary(), new byte[] { 0x30, 0x01, 0x01 }),
            new PdfStreamObject(new PdfDictionary(), new byte[] { 0x30, 0x01, 0x02 }),
            new PdfStreamObject(new PdfDictionary(), new byte[] { 0x30, 0x01, 0x03 }),
            Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(signatureValue)));

        var material = PdfEmbeddedValidationMaterialExtractor.Extract(graph);

        Assert.Single(material.Certificates);
        Assert.Single(material.Crls);
        Assert.Single(material.OcspResponses);
        Assert.Equal(1, material.VriEntryCount);
        Assert.Equal(9, material.TotalBytes);
        var matched = Assert.IsType<PdfEmbeddedVriMaterial>(material.FindForSignature(signatureValue));
        Assert.Single(matched.Certificates);
        Assert.Single(matched.Crls);
        Assert.Single(matched.OcspResponses);
    }

    [Fact]
    public void Extract_ShouldRejectUnsupportedEvidenceStreamFilter()
    {
        var filteredStream = new PdfStreamObject(
            new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Filter"] = new PdfName("UnknownValidationFilter")
            }),
            new byte[] { 0x30, 0x01, 0x01 });
        var graph = CreateGraph(
            filteredStream,
            new PdfStreamObject(new PdfDictionary(), new byte[] { 0x30, 0x01, 0x02 }),
            new PdfStreamObject(new PdfDictionary(), new byte[] { 0x30, 0x01, 0x03 }));

        Assert.Throws<NotSupportedException>(() =>
            PdfEmbeddedValidationMaterialExtractor.Extract(graph));
    }

    private static PdfObjectGraph CreateGraph(
        PdfStreamObject certificate,
        PdfStreamObject crl,
        PdfStreamObject ocsp,
        string vriKey = "0123456789ABCDEF0123456789ABCDEF01234567")
    {
        var catalogId = new PdfObjectId(1, 0);
        var dssId = new PdfObjectId(2, 0);
        var certificateId = new PdfObjectId(3, 0);
        var crlId = new PdfObjectId(4, 0);
        var ocspId = new PdfObjectId(5, 0);
        var vriId = new PdfObjectId(6, 0);

        var vriEntry = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Cert"] = new PdfArray([new PdfReference(certificateId)]),
            ["CRL"] = new PdfArray([new PdfReference(crlId)]),
            ["OCSP"] = new PdfArray([new PdfReference(ocspId)])
        });
        var vri = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            [vriKey] = vriEntry
        });
        var dss = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Certs"] = new PdfArray([new PdfReference(certificateId)]),
            ["CRLs"] = new PdfArray([new PdfReference(crlId)]),
            ["OCSPs"] = new PdfArray([new PdfReference(ocspId)]),
            ["VRI"] = new PdfReference(vriId)
        });
        var catalog = new PdfDictionary(new Dictionary<string, PdfObject>
        {
            ["Type"] = new PdfName("Catalog"),
            ["DSS"] = new PdfReference(dssId)
        });

        var graph = new PdfObjectGraph
        {
            Trailer = new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Root"] = new PdfReference(catalogId)
            })
        };
        Add(graph, catalogId, catalog);
        Add(graph, dssId, dss);
        Add(graph, certificateId, certificate);
        Add(graph, crlId, crl);
        Add(graph, ocspId, ocsp);
        Add(graph, vriId, vri);
        return graph;
    }

    private static void Add(PdfObjectGraph graph, PdfObjectId id, PdfObject value) =>
        graph.Add(new PdfIndirectObject(id, value, new PdfSourceSpan(0, 0)));
}
