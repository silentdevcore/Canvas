using PVAD.Importer.Editing;
using PVAD.Importer.Parsing;
using PVAD.Importer.Security;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Tests;

public sealed class PdfEditableDocumentTests
{
    [Fact]
    public async Task Document_PreservesRuntimeOrderAcrossLoadMoveDeleteCloneAndSave()
    {
        var firstPath = Path.GetTempFileName();
        var secondPath = Path.GetTempFileName();
        var outputPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(firstPath, CreatePdf((200, "A"), (210, "B"), (220, "C")));
            await File.WriteAllBytesAsync(secondPath, CreatePdf((300, "D")));
            using var target = new PdfEditableDocument();
            using var source = new PdfEditableDocument();

            Assert.Equal(PdfPageOperationStatus.Success, target.LoadFromFile(firstPath, false));
            Assert.Equal(PdfPageOperationStatus.Success, source.LoadFromFile(secondPath));
            Assert.Equal(PdfPageOperationStatus.Success, target.MovePage(3, 1));
            Assert.Equal(PdfPageOperationStatus.Success, target.DeletePage(2));
            Assert.Equal(PdfPageOperationStatus.Success, target.ClonePage(source, 1));
            Assert.Equal(PdfPageOperationStatus.Success, target.SaveToFile(outputPath));

            await AssertPages(await File.ReadAllBytesAsync(outputPath),
                (220, "C"), (210, "B"), (300, "D"));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void LoadFromStream_RespectsOwnershipAndReportsFailuresAsStatus()
    {
        var bytes = CreatePdf((200, "A"));
        var stream = new MemoryStream(bytes);
        using var document = new PdfEditableDocument();

        Assert.Equal(PdfPageOperationStatus.Success, document.LoadFromStream(stream, ownStream: true));
        Assert.False(stream.CanRead);
        Assert.Equal(1, document.GetPageCount());
        Assert.Equal(PdfPageOperationStatus.Failed, document.DeletePage(2));
        Assert.NotNull(document.LastError);
    }

    [Fact]
    public async Task Document_CanBuildFromClonesInsertBlankPagesAndMergePaths()
    {
        using var source = new PdfEditableDocument();
        using var target = new PdfEditableDocument();
        using var sourceStream = new MemoryStream(CreatePdf((300, "Source")));
        Assert.Equal(PdfPageOperationStatus.Success, source.LoadFromStream(sourceStream));
        Assert.Equal(PdfPageOperationStatus.Success, target.NewDocument());
        Assert.Equal(PdfPageOperationStatus.Success, target.ClonePage(source, 1));
        Assert.Equal(PdfPageOperationStatus.Success, target.InsertBlankPage(250, 400, 1));

        await AssertPages(target.ToBytes(), (250, null), (300, "Source"));
    }

    [Fact]
    public async Task Document_CanExtractNutrientPageRangeIntoNewDestination()
    {
        using var source = new PdfEditableDocument();
        using var destination = new PdfEditableDocument();
        using var sourceStream = new MemoryStream(CreatePdf((300, "One"), (310, "Two"), (320, "Three")));
        Assert.Equal(PdfPageOperationStatus.Success, source.LoadFromStream(sourceStream));
        Assert.Equal(PdfPageOperationStatus.Success, destination.NewDocument());
        Assert.Equal(PdfPageOperationStatus.Success, destination.ClonePages(source, "2-3"));

        await AssertPages(destination.ToBytes(), (310, "Two"), (320, "Three"));
    }

    [Fact]
    public async Task ClonePages_AppendsToLoadedDestinationAndSelectsLastPage()
    {
        using var source = new PdfEditableDocument();
        using var destination = new PdfEditableDocument();
        using var sourceStream = new MemoryStream(CreatePdf((300, "One"), (310, "Two"), (320, "Three")));
        using var destinationStream = new MemoryStream(CreatePdf((200, "Existing")));
        Assert.Equal(PdfPageOperationStatus.Success, source.LoadFromStream(sourceStream));
        Assert.Equal(PdfPageOperationStatus.Success, destination.LoadFromStream(destinationStream));

        Assert.Equal(PdfPageOperationStatus.Success, destination.ClonePages(source, "1;3"));

        await AssertPages(destination.ToBytes(), (200, "Existing"), (300, "One"), (320, "Three"));
        Assert.Equal(PdfPageOperationStatus.Success,
            destination.SetPageBox(PdfEditablePageBox.CropBox, 10, 480, 190, 20));
        var box = new PdfPageEditor().ReadPageBox(destination.ToBytes(), 3, PdfPageBoxKind.CropBox);
        Assert.Equal(new PdfPageBoxCoordinates(10, 480, 190, 20), box);
    }

    [Fact]
    public async Task PageBox_UsesConfiguredMeasurementUnitForSetAndGet()
    {
        using var document = new PdfEditableDocument();
        using var stream = new MemoryStream(CreatePdf((200, "Measured")));
        Assert.Equal(PdfPageOperationStatus.Success, document.LoadFromStream(stream));
        Assert.Equal(PdfPageOperationStatus.Success,
            document.SetMeasurementUnit(PdfEditableMeasurementUnit.Inch));
        Assert.Equal(PdfPageOperationStatus.Success,
            document.SetPageBox(PdfEditablePageBox.CropBox, 0.25, 6, 2, 0.5));

        float left = 0, top = 0, right = 0, bottom = 0;
        Assert.Equal(PdfPageOperationStatus.Success,
            document.GetPageBox(PdfEditablePageBox.CropBox, ref left, ref top, ref right, ref bottom));
        Assert.Equal(0.25f, left, 3);
        Assert.Equal(6f, top, 3);
        Assert.Equal(2f, right, 3);
        Assert.Equal(0.5f, bottom, 3);

        await using var output = new MemoryStream(document.ToBytes(), writable: false);
        var imported = await Pdf.LoadAsync(output);
        var cropBox = imported.Pages[0].CropBox
            ?? throw new Xunit.Sdk.XunitException("Expected a CropBox after page-box editing.");
        Assert.Equal(18, cropBox.X, 3);
        Assert.Equal(36, cropBox.Y, 3);
        Assert.Equal(126, cropBox.Width, 3);
        Assert.Equal(396, cropBox.Height, 3);
    }

    [Fact]
    public void SaveToFile_AppliesConfiguredAes256Encryption()
    {
        using var document = new PdfEditableDocument();
        using var input = new MemoryStream(CreatePdf((300, "Secret")), writable: false);
        var output = Path.GetTempFileName();
        try
        {
            Assert.Equal(PdfPageOperationStatus.Success, document.LoadFromStream(input));
            Assert.Equal(PdfPageOperationStatus.Success,
                document.ConfigureAes256("owner-secret", "reader-secret"));
            Assert.Equal(PdfPageOperationStatus.Success, document.SaveToFile(output));

            var bytes = File.ReadAllBytes(output);
            var context = new PdfParseContext(bytes, new PdfImporterOptions());
            var table = new PdfCrossReferenceParser().Parse(context);
            Assert.True(PdfDocumentDecryptor.IsEncrypted(table));
            var decrypted = PdfDocumentDecryptor.ParseAndDecrypt(context, table, "reader-secret");
            Assert.NotNull(decrypted.Graph.Trailer);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task MergeDocuments_FromPaths_WritesAllPagesInOrder()
    {
        var first = Path.GetTempFileName();
        var second = Path.GetTempFileName();
        var output = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(first, CreatePdf((200, "First")));
            File.WriteAllBytes(second, CreatePdf((300, "Second")));
            using var utility = new PdfEditableDocument();

            Assert.Equal(PdfPageOperationStatus.Success,
                utility.MergeDocuments([first, second], output));
            await AssertPages(File.ReadAllBytes(output), (200, "First"), (300, "Second"));
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
            File.Delete(output);
        }
    }

    [Fact]
    public void MergeDocuments_WithAttachmentOption_FailsClosed()
    {
        using var first = new PdfEditableDocument();
        using var second = new PdfEditableDocument();
        using var firstStream = new MemoryStream(CreatePdf((200, "First")));
        using var secondStream = new MemoryStream(CreatePdf((300, "Second")));
        Assert.Equal(PdfPageOperationStatus.Success, first.LoadFromStream(firstStream));
        Assert.Equal(PdfPageOperationStatus.Success, second.LoadFromStream(secondStream));
        using var utility = new PdfEditableDocument();

        using var result = utility.MergeDocuments(
            [first, second], PdfEditableMergeOptions.ExcludeAttachments);

        Assert.Equal(PdfPageOperationStatus.Failed, result.GetStatus());
        Assert.IsType<NotSupportedException>(result.LastError);
        Assert.False(result.IsLoaded);
    }

    private static byte[] CreatePdf(params (double Width, string Text)[] pages)
    {
        var document = PVAD.Generator.Pdf.CreateDocument();
        foreach (var page in pages)
            document.AddPage(page.Width, 500).DrawTextFromTop(page.Text, 12, 20, 10);
        return document.ToBytes();
    }

    private static async Task AssertPages(byte[] bytes, params (double Width, string? Text)[] expected)
    {
        await using var stream = new MemoryStream(bytes, writable: false);
        var imported = await Pdf.LoadAsync(stream);
        Assert.Equal(expected.Length, imported.Pages.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Width, imported.Pages[index].MediaBox!.Value.Width, 3);
            if (expected[index].Text is { } text)
                Assert.Contains(imported.Pages[index].TextObjects, item => item.Text == text);
        }
    }
}
