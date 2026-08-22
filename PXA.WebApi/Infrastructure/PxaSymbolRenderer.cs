using System.IO.Compression;
using QRCoder;
using ZXing;
using ZXing.Common;

namespace PXA.WebApi.Infrastructure;

public static class PxaSymbolRenderer
{
    public static byte[] RenderQrCode(string value, int scale = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        scale = Math.Clamp(scale, 1, 32);

        var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.M);
        var modules = data.ModuleMatrix;
        var matrix = new bool[modules.Count, modules[0].Count];
        for (var y = 0; y < modules.Count; y++)
            for (var x = 0; x < modules[y].Count; x++)
                matrix[y, x] = modules[y][x];

        return EncodeMatrix(matrix, scale);
    }

    public static byte[] RenderBarcode(
        string value,
        string? format,
        int width,
        int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        width = Math.Clamp(width, 1, 4096);
        height = Math.Clamp(height, 1, 4096);

        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 2 };
        var matrix = new MultiFormatWriter().encode(value, ParseBarcodeFormat(format), width, height, hints);
        var pixels = new bool[matrix.Height, matrix.Width];
        for (var y = 0; y < matrix.Height; y++)
            for (var x = 0; x < matrix.Width; x++)
                pixels[y, x] = matrix[x, y];

        return EncodeMatrix(pixels, 1);
    }

    private static BarcodeFormat ParseBarcodeFormat(string? type) => type?.ToLowerInvariant() switch
    {
        "code128" or "code-128" => BarcodeFormat.CODE_128,
        "code39" or "code-39" => BarcodeFormat.CODE_39,
        "ean13" or "ean-13" => BarcodeFormat.EAN_13,
        "ean8" or "ean-8" => BarcodeFormat.EAN_8,
        "upca" or "upc-a" => BarcodeFormat.UPC_A,
        "pdf417" => BarcodeFormat.PDF_417,
        _ => BarcodeFormat.CODE_128,
    };

    private static byte[] EncodeMatrix(bool[,] matrix, int scale)
    {
        var rows = matrix.GetLength(0);
        var columns = matrix.GetLength(1);
        var width = columns * scale;
        var height = rows * scale;
        var raw = new byte[height * (1 + width)];
        var offset = 0;
        for (var y = 0; y < height; y++)
        {
            raw[offset++] = 0;
            var row = y / scale;
            for (var x = 0; x < width; x++)
                raw[offset++] = matrix[row, x / scale] ? (byte)0 : (byte)255;
        }

        byte[] compressed;
        using (var buffer = new MemoryStream())
        {
            using (var zlib = new ZLibStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
                zlib.Write(raw);
            compressed = buffer.ToArray();
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", [
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            8, 0, 0, 0, 0,
        ]);
        WriteChunk(png, "IDAT", compressed);
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = data.Length;
        stream.Write([(byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length]);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crcInput = new byte[4 + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, 4);
        var crc = Crc32(crcInput);
        stream.Write([(byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc]);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
