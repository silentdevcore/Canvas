using System.Security.Cryptography;
using System.Text;
using PXA.Importer;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Trust;
using PXA.Importer.Xref;

namespace PXA.Importer.Tests;

public sealed class PdfRevisionAndChangeImpactTests
{
    [Fact]
    public void CrossReferenceParser_ShouldExposeChronologicalRevisionChainAndHashes()
    {
        var bytes = BuildIncrementalPdf();
        var table = new PdfCrossReferenceParser().Parse(
            new PdfParseContext(bytes, new PdfImporterOptions()));

        Assert.Empty(table.Diagnostics);
        Assert.Collection(
            table.Revisions,
            first =>
            {
                Assert.Equal(0, first.SequenceNumber);
                Assert.Equal(1, first.DistanceFromLatest);
                Assert.Null(first.PreviousCrossReferenceOffset);
                Assert.Equal(PdfCrossReferenceSectionKind.ClassicTable, first.Kind);
                Assert.Equal(
                    Convert.ToHexStringLower(SHA256.HashData(bytes.AsSpan(0, checked((int)first.EndOffset)))),
                    first.Sha256);
            },
            second =>
            {
                Assert.Equal(1, second.SequenceNumber);
                Assert.Equal(0, second.DistanceFromLatest);
                Assert.Equal(table.Revisions[0].CrossReferenceOffset, second.PreviousCrossReferenceOffset);
                Assert.Equal(bytes.Length, second.EndOffset);
                Assert.Equal(
                    Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    second.Sha256);
            });
    }

    [Fact]
    public void CrossReferenceParser_ShouldRejectCyclicPreviousRevisionChain()
    {
        var bytes = BuildCyclicRevisionPdf();
        var table = new PdfCrossReferenceParser().Parse(
            new PdfParseContext(bytes, new PdfImporterOptions()));

        var diagnostic = Assert.Single(table.Diagnostics);
        Assert.Equal("PXAPDFXREF003", diagnostic.Code);
    }

    [Fact]
    public void CrossReferenceParser_ShouldRejectNonIntegerPreviousRevisionReference()
    {
        var bytes = BuildInvalidPreviousRevisionPdf();
        var table = new PdfCrossReferenceParser().Parse(
            new PdfParseContext(bytes, new PdfImporterOptions()));

        Assert.Contains(table.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFXREF004");
    }

    [Fact]
    public void ChangeImpactAnalyzer_ShouldRequireValidationForIncrementalMutation()
    {
        var report = CreateTrustReport();

        var impact = new PdfChangeImpactAnalyzer().Analyze(
            report,
            PdfSaveStrategy.IncrementalUpdate,
            PdfMutationKind.FillForm);

        Assert.True(impact.RequiresExplicitConfirmation);
        Assert.Equal(PdfChangeImpactStatus.RequiresValidation, Assert.Single(impact.Signatures).Status);
        Assert.Equal(PdfChangeImpactStatus.RequiresValidation, Assert.Single(impact.ComplianceClaims).Status);
        Assert.Contains(impact.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST502");
    }

    [Fact]
    public void ChangeImpactAnalyzer_ShouldInvalidateSignaturesForFullRewrite()
    {
        var impact = new PdfChangeImpactAnalyzer().Analyze(
            CreateTrustReport(),
            PdfSaveStrategy.FullRewrite,
            PdfMutationKind.ApplyRedaction);

        Assert.True(impact.RequiresExplicitConfirmation);
        Assert.Equal(PdfChangeImpactStatus.Invalidated, Assert.Single(impact.Signatures).Status);
        Assert.Contains(impact.Diagnostics, diagnostic => diagnostic.Code == "PXAPDFTRUST501");
    }

    [Fact]
    public void IncrementalWriter_ShouldPreserveSourceBytesAndAppendValidatedRevision()
    {
        var source = BuildIncrementalPdf();
        var update = new PdfIncrementalObjectUpdate(
            new PdfObjectId(3, 0),
            new PdfDictionary
            {
                ["Type"] = new PdfName("PXA Test"),
                ["Value"] = new PdfString("updated"u8.ToArray(), IsHex: false)
            });

        var result = new PdfIncrementalUpdateWriter().Append(source, [update]);

        Assert.True(result.Bytes.AsSpan(0, source.Length).SequenceEqual(source));
        Assert.Equal(3, result.RevisionCount);
        Assert.Equal([update.Id], result.ChangedObjects);
        Assert.True(result.CrossReferenceOffset > result.PreviousCrossReferenceOffset);

        var table = new PdfCrossReferenceParser().Parse(
            new PdfParseContext(result.Bytes, new PdfImporterOptions()));
        Assert.Empty(table.Diagnostics);
        Assert.Equal(3, table.Revisions.Count);
        Assert.Contains(update.Id, table.Entries.Keys);
    }

    [Fact]
    public void IncrementalWriter_ShouldRejectInvalidSourceRevisionChain()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            new PdfIncrementalUpdateWriter().Append(
                BuildCyclicRevisionPdf(),
                [new PdfIncrementalObjectUpdate(new PdfObjectId(2, 0), new PdfInteger(1))]));

        Assert.Contains("PXAPDFXREF003", exception.Message, StringComparison.Ordinal);
    }

    private static PdfTrustReport CreateTrustReport() => new(
        PdfTrustStatus.Indeterminate,
        PdfTrustStatus.Valid,
        new string('a', 64),
        100,
        "1.7",
        1,
        3,
        new PdfEncryptionInspection(false, PdfTrustStatus.NotApplicable),
        [new PdfSignatureInspection(3, 0, PdfTrustStatus.Indeterminate, "ETSI.CAdES.detached", [0, 50, 80, 20], 4096)],
        new PdfComplianceClaim("PDF/A", "PDF/A-2b", true, PdfTrustStatus.Indeterminate, "test"),
        new PdfComplianceClaim("PDF/UA", null, false, PdfTrustStatus.NotApplicable, "test"),
        [],
        []);

    private static byte[] BuildIncrementalPdf()
    {
        var builder = new StringBuilder("%PDF-1.7\n");
        var objectOneOffset = builder.Length;
        builder.Append("1 0 obj\n<< /Type /Catalog >>\nendobj\n");
        var firstXrefOffset = builder.Length;
        builder.Append("xref\n0 2\n");
        builder.Append("0000000000 65535 f \n");
        builder.Append($"{objectOneOffset:0000000000} 00000 n \n");
        builder.Append("trailer\n<< /Size 2 /Root 1 0 R >>\nstartxref\n");
        builder.Append(firstXrefOffset).Append("\n%%EOF\n");

        var objectTwoOffset = builder.Length;
        builder.Append("2 0 obj\n<< /Producer (PXA) >>\nendobj\n");
        var secondXrefOffset = builder.Length;
        builder.Append("xref\n2 1\n");
        builder.Append($"{objectTwoOffset:0000000000} 00000 n \n");
        builder.Append($"trailer\n<< /Size 3 /Root 1 0 R /Prev {firstXrefOffset} >>\nstartxref\n");
        builder.Append(secondXrefOffset).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static byte[] BuildCyclicRevisionPdf()
    {
        var builder = new StringBuilder("%PDF-1.7\n");
        var objectOffset = builder.Length;
        builder.Append("1 0 obj\n<< /Type /Catalog >>\nendobj\n");
        var xrefOffset = builder.Length;
        builder.Append("xref\n0 2\n");
        builder.Append("0000000000 65535 f \n");
        builder.Append($"{objectOffset:0000000000} 00000 n \n");
        builder.Append($"trailer\n<< /Size 2 /Root 1 0 R /Prev {xrefOffset} >>\nstartxref\n");
        builder.Append(xrefOffset).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static byte[] BuildInvalidPreviousRevisionPdf()
    {
        var builder = new StringBuilder("%PDF-1.7\n");
        var objectOffset = builder.Length;
        builder.Append("1 0 obj\n<< /Type /Catalog >>\nendobj\n");
        var xrefOffset = builder.Length;
        builder.Append("xref\n0 2\n");
        builder.Append("0000000000 65535 f \n");
        builder.Append($"{objectOffset:0000000000} 00000 n \n");
        builder.Append("trailer\n<< /Size 2 /Root 1 0 R /Prev /invalid >>\nstartxref\n");
        builder.Append(xrefOffset).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
