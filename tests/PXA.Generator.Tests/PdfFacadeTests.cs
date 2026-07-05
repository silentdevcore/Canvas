using Canvas.Pdf;
using PXA.Generator;

namespace PXA.Generator.Tests;

public sealed class PdfFacadeTests
{
    [Fact]
    public void CreateDocument_ReturnsCanvasPdfDocument()
    {
        var document = Pdf.CreateDocument();

        Assert.IsType<PdfDocument>(document);
        var page = document.AddPage(300, 180);
        page.DrawTextFromTop("PXA generator facade", 24, 24, 12);

        var bytes = document.ToBytes();

        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public void CreateDocument_PreservesDefaultFontOption()
    {
        var document = Pdf.CreateDocument(PdfStandardFont.Courier);

        Assert.Equal(PdfStandardFont.Courier, document.DefaultFont);
    }
}
