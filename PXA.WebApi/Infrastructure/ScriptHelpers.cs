using QRCoder;
using ZXing;
using ZXing.Common;
using System.IO.Compression;

namespace PXA.WebApi.Infrastructure;

/// <summary>
/// Public helpers callable from Roslyn scripts (csharp-code-to-pdf endpoint).
/// </summary>
public static class ScriptHelpers
{
    public static byte[] GenerateQrPng(string value, int scale = 10)
    {
        var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(value, QRCodeGenerator.ECCLevel.M);
        var modules = data.ModuleMatrix;
        var size    = modules.Count;
        var px      = size * scale;

        // Build 8-bit grayscale raw rows (filter byte + pixel bytes per row)
        var raw = new byte[px * (1 + px)];
        var p = 0;
        for (var y = 0; y < px; y++)
        {
            raw[p++] = 0; // filter: None
            var row = y / scale;
            for (var x = 0; x < px; x++)
                raw[p++] = modules[row][x / scale] ? (byte)0 : (byte)255;
        }
        return EncodeGrayscalePng(raw, px, px);
    }

    public static byte[] GenerateBarcodePng(string value, string? format, int width, int height)
    {
        var barcodeFormat = format?.ToLowerInvariant() switch
        {
            "code39"  or "code-39"  => BarcodeFormat.CODE_39,
            "ean13"   or "ean-13"   => BarcodeFormat.EAN_13,
            "ean8"    or "ean-8"    => BarcodeFormat.EAN_8,
            "upca"    or "upc-a"    => BarcodeFormat.UPC_A,
            "pdf417"                => BarcodeFormat.PDF_417,
            _                       => BarcodeFormat.CODE_128,
        };
        var hints  = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 2 };
        var matrix = new MultiFormatWriter().encode(value, barcodeFormat, width, height, hints);
        var w = matrix.Width;
        var h = matrix.Height;
        var raw = new byte[h * (1 + w)];
        var p = 0;
        for (var y = 0; y < h; y++)
        {
            raw[p++] = 0;
            for (var x = 0; x < w; x++)
                raw[p++] = matrix[x, y] ? (byte)0 : (byte)255;
        }
        return EncodeGrayscalePng(raw, w, h);
    }

    // Encodes pre-built filter+pixel rows as an 8-bit grayscale PNG
    private static byte[] EncodeGrayscalePng(byte[] raw, int w, int h)
    {
        byte[] idat;
        using (var ms = new MemoryStream())
        {
            using (var zlib = new ZLibStream(ms, CompressionLevel.Fastest, leaveOpen: true))
                zlib.Write(raw);
            idat = ms.ToArray();
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WritePngChunk(png, "IHDR", [
            (byte)(w >> 24), (byte)(w >> 16), (byte)(w >> 8), (byte)w,
            (byte)(h >> 24), (byte)(h >> 16), (byte)(h >> 8), (byte)h,
            8, 0, 0, 0, 0  // 8-bit grayscale
        ]);
        WritePngChunk(png, "IDAT", idat);
        WritePngChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WritePngChunk(Stream s, string type, byte[] data)
    {
        var len = data.Length;
        s.Write(new[] { (byte)(len >> 24), (byte)(len >> 16), (byte)(len >> 8), (byte)len });
        var tb = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(tb);
        s.Write(data);
        var crcIn = new byte[4 + data.Length];
        tb.CopyTo(crcIn, 0);
        data.CopyTo(crcIn, 4);
        var crc = Crc32(crcIn);
        s.Write(new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc });
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
