using PXA.Importer.Editing;

namespace PXA.Importer.Tests;

public sealed class PdfPageComposerTests
{
    [Fact]
    public async Task Apply_ClonesCompletePageContentAtRequestedPosition()
    {
        var target = CreatePdf((200, "Target one"), (210, "Target two"));
        var source = CreatePdf((300, "Source one"), (310, "Source two"));

        var result = new PdfPageComposer().Apply(target,
        [
            new PdfPageImport(source, PageNumber: 2, TargetPageNumber: 2)
        ]);

        Assert.Equal(2, result.OriginalPageCount);
        Assert.Equal(3, result.PageCount);
        await AssertPages(result.Bytes,
            (200, "Target one"),
            (310, "Source two"),
            (210, "Target two"));
    }

    [Fact]
    public async Task Merge_AppendsEveryPageInDocumentOrder()
    {
        var first = CreatePdf((200, "First"));
        var second = CreatePdf((300, "Second one"), (310, "Second two"));
        var third = CreatePdf((400, "Third"));

        var result = new PdfPageComposer().Merge([first, second, third]);

        Assert.Equal(4, result.PageCount);
        await AssertPages(result.Bytes,
            (200, "First"),
            (300, "Second one"),
            (310, "Second two"),
            (400, "Third"));
    }

    [Fact]
    public void Apply_RejectsEncryptedInputsAndInvalidPageNumbers()
    {
        var target = CreatePdf((200, "Target"));
        var source = CreatePdf((300, "Source"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfPageComposer().Apply(target, [new PdfPageImport(source, 2)]));

        var encryptedDocument = PXA.Generator.Pdf.CreateDocument();
        encryptedDocument.AddPage(300, 500).DrawTextFromTop("Encrypted", 12, 20, 10);
        var encrypted = encryptedDocument.ToBytes(new PXA.Pdf.PdfSaveOptions
        {
            Encryption = new PXA.Pdf.PdfEncryptionOptions
            {
                OwnerPassword = "owner-secret",
                UserPassword = "reader-secret",
                Algorithm = PXA.Pdf.PdfEncryptionAlgorithm.Aes256
            }
        });
        Assert.Throws<NotSupportedException>(() =>
            new PdfPageComposer().Apply(target, [new PdfPageImport(encrypted, 1)]));
    }

    [Fact]
    public async Task Split_ExtractsRangesAsIndependentLosslessDocuments()
    {
        var source = CreatePdf((200, "First"), (210, "Second"), (220, "Third"));

        var result = new PdfPageComposer().Split(source,
        [
            new PdfPageRange(1, 2),
            new PdfPageRange(3, 3)
        ]);

        Assert.Equal(2, result.Documents.Count);
        await AssertPages(result.Documents[0], (200, "First"), (210, "Second"));
        await AssertPages(result.Documents[1], (220, "Third"));
    }

    [Fact]
    public async Task AppendPages_AddsSelectedPagesToExistingTargetInRangeOrder()
    {
        var target = CreatePdf((150, "Existing"));
        var source = CreatePdf((200, "First"), (210, "Second"), (220, "Third"));

        var result = new PdfPageComposer().AppendPages(target, source, [3, 1]);

        Assert.Equal(1, result.OriginalPageCount);
        Assert.Equal(3, result.PageCount);
        await AssertPages(result.Bytes, (150, "Existing"), (220, "Third"), (200, "First"));
    }

    [Fact]
    public void Split_RejectsInvalidRangesAndDuplicateExtractionPages()
    {
        var source = CreatePdf((200, "First"), (210, "Second"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfPageComposer().Split(source, [new PdfPageRange(0, 1)]));
        Assert.Throws<ArgumentException>(() =>
            new PdfPageComposer().ExtractPages(source, [1, 1]));
        Assert.Throws<FormatException>(() =>
            PdfPageRangeParser.Parse("2-1", 2));
    }

    private static byte[] CreatePdf(params (double Width, string Text)[] pages)
    {
        var document = PXA.Generator.Pdf.CreateDocument();
        foreach (var page in pages)
            document.AddPage(page.Width, 500).DrawTextFromTop(page.Text, 12, 20, 10);
        return document.ToBytes();
    }

    private static async Task AssertPages(byte[] bytes, params (double Width, string Text)[] expected)
    {
        await using var stream = new MemoryStream(bytes, writable: false);
        var imported = await Pdf.LoadAsync(stream);
        Assert.Equal(expected.Length, imported.Pages.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Width, imported.Pages[index].MediaBox!.Value.Width, 3);
            Assert.Contains(imported.Pages[index].TextObjects, text => text.Text == expected[index].Text);
        }
    }
}
