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
