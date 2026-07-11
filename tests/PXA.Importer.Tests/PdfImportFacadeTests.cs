namespace PXA.Importer.Tests;

public sealed class PdfImportFacadeTests
{
    [Fact]
    public async Task LoadAsync_ReturnsImportedPdfDocument()
    {
        var source = PXA.Generator.Pdf.CreateDocument();
        var page = source.AddPage(300, 180);
        page.DrawTextFromTop("PXA importer facade", 24, 24, 12);

        await using var stream = new MemoryStream(source.ToBytes());

        var imported = await Pdf.LoadAsync(stream);

        var importedPage = Assert.Single(imported.Pages);
        Assert.Contains(importedPage.TextObjects, text => text.Text == "PXA importer facade");
    }

    [Fact]
    public async Task LoadAsync_AcceptsPxaImportOptions()
    {
        var source = PXA.Generator.Pdf.CreateDocument();
        source.AddPage(300, 180).DrawTextFromTop("Options", 24, 24, 12);
        await using var stream = new MemoryStream(source.ToBytes());

        var imported = await Pdf.LoadAsync(stream, new PdfImportOptions
        {
            LazyObjectLoading = false,
            DeferredStreamDecoding = false,
            ParsePagesInParallel = false,
            MaxParallelPageParsers = 1,
        });

        Assert.Single(imported.Pages);
    }

    [Fact]
    public async Task PxaImporterClass_StillLoadsPdf()
    {
        var source = PXA.Generator.Pdf.CreateDocument();
        source.AddPage(300, 180).DrawTextFromTop("PXA importer class compatibility", 24, 24, 12);
        await using var stream = new MemoryStream(source.ToBytes());

        var imported = await new PdfImporter().LoadAsync(stream);

        var importedPage = Assert.Single(imported.Pages);
        Assert.Contains(importedPage.TextObjects, text => text.Text == "PXA importer class compatibility");
    }
}
