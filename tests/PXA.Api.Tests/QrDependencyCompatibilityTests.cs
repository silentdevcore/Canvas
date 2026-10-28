using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PXA.Infrastructure.Word;
using PXA.WebApi.Infrastructure;
using QRCoder;
using ZXing;
using ZXing.Common;

namespace PXA.Api.Tests;

public sealed class QrDependencyCompatibilityTests
{
    [Fact]
    public void Script_qr_png_uses_the_image_format_supported_by_the_pdf_writer()
    {
        var png = ScriptHelpers.GenerateQrPng("synthetic QR");
        Assert.Equal(8, png[24]);
        Assert.Equal(0, png[25]);
#pragma warning disable PXA0001 // Test the same low-level image reader used by the API mapper.
        var document = new PXA.Pdf.PdfDocument();
#pragma warning restore PXA0001
        document.AddPage().DrawImage(png, 0, 0, 100, 100);
        Assert.Contains("/Subtype /Image", Encoding.ASCII.GetString(document.ToBytes()));
    }

    [Theory]
    [InlineData("012345678901234567890123456789")]
    [InlineData("https://example.test/receipt?reference=ABC-42&amount=123.45")]
    [InlineData("Gr\u00fc\u00dfe / \u0645\u0631\u062d\u0628\u0627 / \u65e5\u672c\u8a9e")]
    public void Generated_modules_decode_to_the_original_payload(string value)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.M);
        var modules = data.ModuleMatrix;
        const int scale = 4;
        var size = modules.Count * scale;
        var pixels = new byte[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = modules[y / scale][x / scale] ? (byte)0 : (byte)255;
        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.QR_CODE], TryHarder = true },
        };
        var result = reader.Decode(new RGBLuminanceSource(pixels, size, size,
            RGBLuminanceSource.BitmapFormat.Gray8));

        Assert.NotNull(result);
        Assert.Equal(value, result.Text);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("https://example.test/receipt?reference=ABC-42")]
    [InlineData("Gr\u00fc\u00dfe / \u0645\u0631\u062d\u0628\u0627")]
    public void Pdf_export_contains_a_real_qr_image_instead_of_a_text_fallback(string value)
    {
        var document = DesignJsonMapper.MapToPdfDocument(Design(value));
        var bytes = document.ToBytes();

        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes));
        Assert.Contains("/Subtype /Image", Encoding.ASCII.GetString(bytes));
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("https://example.test/receipt?reference=ABC-42")]
    [InlineData("Gr\u00fc\u00dfe / \u0645\u0631\u062d\u0628\u0627")]
    public void Word_export_contains_a_png_qr_image(string value)
    {
        var bytes = new WordDocumentExporter().Export(Design(value), new ExportOptions());
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var image = Assert.Single(document.MainDocumentPart!.ImageParts);
        Assert.Equal("image/png", image.ContentType);
        using var png = image.GetStream();
        var signature = new byte[8];
        png.ReadExactly(signature);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, signature);
    }

    private static DesignExportDto Design(string value) => new()
    {
        Id = "synthetic-qr-compatibility", Name = "Synthetic QR",
        Pages = [new PageDto { Id = "page", Elements = [new ElementDto
        {
            Id = "qr", Type = "qrcode", X = 20, Y = 20, Width = 100, Height = 100, QrValue = value,
        }] }],
    };
}
