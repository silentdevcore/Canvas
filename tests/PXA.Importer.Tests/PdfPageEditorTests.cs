using System.Security.Cryptography;
using PXA.Importer.Editing;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Saving;
using PXA.Importer.Xref;

namespace PXA.Importer.Tests;

public sealed class PdfPageEditorTests
{
    [Fact]
    public async Task Apply_MovesAndDeletesPagesWithoutRegeneratingTheirContent()
    {
        var source = CreateThreePagePdf();
        var sourceHash = SHA256.HashData(source);

        var result = new PdfPageEditor().Apply(source,
        [
            new PdfMovePageOperation(3, 1),
            new PdfDeletePageOperation(2)
        ]);

        Assert.Equal(3, result.OriginalPageCount);
        Assert.Equal(2, result.PageCount);
        Assert.Equal(sourceHash, SHA256.HashData(source));
        Assert.NotEqual(sourceHash, SHA256.HashData(result.Bytes));

        await using var stream = new MemoryStream(result.Bytes, writable: false);
        var imported = await Pdf.LoadAsync(stream);
        Assert.Collection(imported.Pages,
            page =>
            {
                Assert.Equal(333, page.MediaBox!.Value.Width, 3);
                Assert.Contains(page.TextObjects, text => text.Text == "Page three");
            },
            page =>
            {
                Assert.Equal(222, page.MediaBox!.Value.Width, 3);
                Assert.Contains(page.TextObjects, text => text.Text == "Page two");
            });
    }

    [Fact]
    public async Task Apply_DeletePreservesRemainingPageGeometryAndResources()
    {
        var result = new PdfPageEditor().Apply(
            CreateThreePagePdf(),
            [new PdfDeletePageOperation(2)]);

        await using var stream = new MemoryStream(result.Bytes, writable: false);
        var imported = await Pdf.LoadAsync(stream);
        Assert.Collection(imported.Pages,
            page =>
            {
                Assert.Equal(111, page.MediaBox!.Value.Width, 3);
                Assert.Contains(page.TextObjects, text => text.Text == "Page one");
            },
            page =>
            {
                Assert.Equal(333, page.MediaBox!.Value.Width, 3);
                Assert.Contains(page.TextObjects, text => text.Text == "Page three");
            });
    }

    [Fact]
    public void Apply_RejectsInvalidPageNumbersAndDeletingTheLastPage()
    {
        var threePages = CreateThreePagePdf();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfPageEditor().Apply(threePages, [new PdfMovePageOperation(4, 1)]));

        var onePage = PXA.Generator.Pdf.CreateDocument();
        onePage.AddPage(100, 100);
        Assert.Throws<InvalidOperationException>(() =>
            new PdfPageEditor().Apply(onePage.ToBytes(), [new PdfDeletePageOperation(1)]));
    }

    [Fact]
    public void Apply_RequiresAtLeastOneOperation()
    {
        Assert.Throws<ArgumentException>(() =>
            new PdfPageEditor().Apply(CreateThreePagePdf(), []));
    }

    [Fact]
    public async Task Apply_SetsCropBoxWithoutRebuildingPageContent()
    {
        var source = CreateThreePagePdf();
        var result = new PdfPageEditor().Apply(source,
        [
            new PdfSetPageBoxOperation(1, PdfPageBoxKind.CropBox, 10, 180, 90, 20)
        ]);

        var box = new PdfPageEditor().ReadPageBox(result.Bytes, 1, PdfPageBoxKind.CropBox);
        Assert.Equal(new PdfPageBoxCoordinates(10, 180, 90, 20), box);

        await using var stream = new MemoryStream(result.Bytes, writable: false);
        var imported = await Pdf.LoadAsync(stream);
        Assert.Equal(10, imported.Pages[0].CropBox?.X);
        Assert.Equal(20, imported.Pages[0].CropBox?.Y);
        Assert.Equal(80, imported.Pages[0].CropBox?.Width);
        Assert.Equal(160, imported.Pages[0].CropBox?.Height);
        Assert.Contains(imported.Pages[0].TextObjects, text => text.Text == "Page one");
    }

    [Fact]
    public void Apply_RejectsCropOutsideMediaBoxAndEncryptedCrop()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfPageEditor().Apply(CreateThreePagePdf(),
            [new PdfSetPageBoxOperation(1, PdfPageBoxKind.CropBox, -1, 180, 90, 20)]));

        var document = PXA.Generator.Pdf.CreateDocument();
        document.AddPage(200, 200);
        var encrypted = document.ToBytes(new PXA.Pdf.PdfSaveOptions
        {
            Encryption = new PXA.Pdf.PdfEncryptionOptions
            {
                OwnerPassword = "owner-secret",
                UserPassword = "reader-secret",
                Algorithm = PXA.Pdf.PdfEncryptionAlgorithm.Aes256
            }
        });
        Assert.Throws<NotSupportedException>(() =>
            new PdfPageEditor().Apply(encrypted,
            [new PdfSetPageBoxOperation(1, PdfPageBoxKind.CropBox, 10, 100, 100, 10)]));
    }

    [Fact]
    public void Apply_RejectsDeletingAPageThatRemainsReachableOutsideThePageTree()
    {
        var source = CreateThreePagePdf();
        var context = new PdfParseContext(source, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        var catalogReference = Assert.IsType<PdfReference>(graph.Trailer!["Root"]);
        var catalog = Assert.IsType<PdfDictionary>(graph.Resolve(catalogReference.Id)!.Value);
        var pagesReference = Assert.IsType<PdfReference>(catalog["Pages"]);
        var pages = Assert.IsType<PdfDictionary>(graph.Resolve(pagesReference.Id)!.Value);
        var pageReferences = Assert.IsType<PdfArray>(pages["Kids"]).Items
            .Cast<PdfReference>()
            .ToArray();
        var updatedCatalog = new PdfDictionary(catalog.Values.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value,
            StringComparer.Ordinal))
        {
            ["PxaExternalDestination"] = pageReferences[1]
        };
        var withExternalReference = new PdfIncrementalUpdateWriter().Append(source,
        [
            new PdfIncrementalObjectUpdate(catalogReference.Id, updatedCatalog)
        ]).Bytes;

        var error = Assert.Throws<NotSupportedException>(() =>
            new PdfPageEditor().Apply(withExternalReference, [new PdfDeletePageOperation(2)]));

        Assert.Contains("still referenced", error.Message, StringComparison.Ordinal);
    }

    private static byte[] CreateThreePagePdf()
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        document.AddPage(111, 400).DrawTextFromTop("Page one", 12, 20, 10);
        document.AddPage(222, 500).DrawTextFromTop("Page two", 12, 20, 10);
        document.AddPage(333, 600).DrawTextFromTop("Page three", 12, 20, 10);
        return document.ToBytes();
    }
}
