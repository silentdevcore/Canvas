using System.Buffers.Binary;
using System.IO.Compression;

namespace PXA.Pdf;

internal static class PdfImageReader
{
    public static PdfImageData Read(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path cannot be null or empty.", nameof(imagePath));
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Image file not found.", imagePath);
        }

        var data = File.ReadAllBytes(imagePath);

        return Read(data);
    }

    public static PdfImageData Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Read(memory.ToArray());
    }

    public static PdfImageData Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length == 0)
        {
            throw new ArgumentException("Image data cannot be empty.", nameof(data));
        }

        data = data.ToArray();

        if (IsPng(data))
        {
            return ReadPng(data);
        }

        if (IsJpeg(data))
        {
            return ReadJpeg(data);
        }

        throw new NotSupportedException("Only PNG and JPEG images are supported.");
    }

    private static PdfImageData ReadPng(byte[] data)
    {
        var offset = 8;
        var width = 0;
        var height = 0;
        var bitDepth = 0;
        var colorType = 0;
        var compressionMethod = 0;
        var filterMethod = 0;
        var interlaceMethod = 0;
        var idatData = new List<byte>();

        while (offset + 8 <= data.Length)
        {
            var length = ReadInt32BigEndian(data, offset);
            offset += 4;

            var chunkType = ReadChunkType(data, offset);
            offset += 4;

            if (offset + length + 4 > data.Length)
            {
                throw new InvalidDataException("Invalid PNG chunk length.");
            }

            if (chunkType == "IHDR")
            {
                if (length != 13)
                {
                    throw new InvalidDataException("Invalid PNG IHDR length.");
                }

                width = ReadInt32BigEndian(data, offset);
                height = ReadInt32BigEndian(data, offset + 4);
                bitDepth = data[offset + 8];
                colorType = data[offset + 9];
                compressionMethod = data[offset + 10];
                filterMethod = data[offset + 11];
                interlaceMethod = data[offset + 12];
            }
            else if (chunkType == "IDAT")
            {
                idatData.AddRange(data.AsSpan(offset, length).ToArray());
            }
            else if (chunkType == "IEND")
            {
                break;
            }

            offset += length + 4;
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("PNG IHDR chunk is missing or invalid.");
        }

        if (idatData.Count == 0)
        {
            throw new InvalidDataException("PNG IDAT chunk is missing.");
        }

        if (compressionMethod != 0 || filterMethod != 0 || interlaceMethod != 0)
        {
            throw new NotSupportedException("Only non-interlaced PNG with standard compression/filter methods is supported.");
        }

        if (bitDepth != 8)
        {
            throw new NotSupportedException("Only 8-bit PNG images are supported.");
        }

        if (colorType is 4 or 6)
        {
            return ReadPngWithAlpha(width, height, colorType, idatData.ToArray());
        }

        var (colors, colorSpaceName) = colorType switch
        {
            0 => (1, "DeviceGray"),
            2 => (3, "DeviceRGB"),
            _ => throw new NotSupportedException("PNG color type is not supported. Supported: grayscale/RGB with or without alpha.")
        };

        return new PdfImageData
        {
            Width = width,
            Height = height,
            BitsPerComponent = 8,
            ColorSpaceName = colorSpaceName,
            FilterName = "FlateDecode",
            DecodeParameters = $"/Predictor 15 /Colors {colors} /BitsPerComponent 8 /Columns {width}",
            Data = idatData.ToArray()
        };
    }

    private static PdfImageData ReadPngWithAlpha(int width, int height, int colorType, byte[] compressedData)
    {
        var bytesPerPixel = colorType == 6 ? 4 : 2;
        var colorBytesPerPixel = colorType == 6 ? 3 : 1;
        var colors = colorType == 6 ? 3 : 1;
        var colorSpaceName = colorType == 6 ? "DeviceRGB" : "DeviceGray";
        var rowLength = width * bytesPerPixel;
        var expectedLength = height * (1 + rowLength);

        var decompressed = DecompressZlib(compressedData);

        if (decompressed.Length != expectedLength)
        {
            throw new InvalidDataException("Unexpected PNG data length.");
        }

        var previousRow = new byte[rowLength];
        var colorScanlines = new byte[height * (1 + (width * colorBytesPerPixel))];
        var alphaScanlines = new byte[height * (1 + width)];

        for (var row = 0; row < height; row++)
        {
            var sourceOffset = row * (1 + rowLength);
            var filterType = decompressed[sourceOffset];
            var filteredRow = decompressed.AsSpan(sourceOffset + 1, rowLength);
            var currentRow = new byte[rowLength];
            UnfilterPngRow(filterType, filteredRow, previousRow, currentRow, bytesPerPixel);

            var colorOffset = row * (1 + (width * colorBytesPerPixel));
            colorScanlines[colorOffset] = 0;
            var alphaOffset = row * (1 + width);
            alphaScanlines[alphaOffset] = 0;

            for (var x = 0; x < width; x++)
            {
                var pixelOffset = x * bytesPerPixel;

                if (colorType == 6)
                {
                    var colorPixelOffset = colorOffset + 1 + (x * 3);
                    colorScanlines[colorPixelOffset] = currentRow[pixelOffset];
                    colorScanlines[colorPixelOffset + 1] = currentRow[pixelOffset + 1];
                    colorScanlines[colorPixelOffset + 2] = currentRow[pixelOffset + 2];
                    alphaScanlines[alphaOffset + 1 + x] = currentRow[pixelOffset + 3];
                }
                else
                {
                    colorScanlines[colorOffset + 1 + x] = currentRow[pixelOffset];
                    alphaScanlines[alphaOffset + 1 + x] = currentRow[pixelOffset + 1];
                }
            }

            previousRow = currentRow;
        }

        var colorData = CompressZlib(colorScanlines);
        var alphaData = CompressZlib(alphaScanlines);

        var decodeParameters = $"/Predictor 15 /Colors {colors} /BitsPerComponent 8 /Columns {width}";
        var alphaDecodeParameters = $"/Predictor 15 /Colors 1 /BitsPerComponent 8 /Columns {width}";

        return new PdfImageData
        {
            Width = width,
            Height = height,
            BitsPerComponent = 8,
            ColorSpaceName = colorSpaceName,
            FilterName = "FlateDecode",
            DecodeParameters = decodeParameters,
            Data = colorData,
            SoftMask = new PdfImageData
            {
                Width = width,
                Height = height,
                BitsPerComponent = 8,
                ColorSpaceName = "DeviceGray",
                FilterName = "FlateDecode",
                DecodeParameters = alphaDecodeParameters,
                Data = alphaData
            }
        };
    }

    private static PdfImageData ReadJpeg(byte[] data)
    {
        var offset = 0;

        if (!IsJpeg(data))
        {
            throw new InvalidDataException("Invalid JPEG header.");
        }

        offset += 2;

        while (offset + 3 < data.Length)
        {
            while (offset < data.Length && data[offset] == 0xFF)
            {
                offset++;
            }

            if (offset >= data.Length)
            {
                break;
            }

            var marker = data[offset++];

            if (marker == 0xD9)
            {
                break;
            }

            if (marker is >= 0xD0 and <= 0xD7 || marker == 0x01)
            {
                continue;
            }

            if (offset + 1 >= data.Length)
            {
                break;
            }

            var segmentLength = (data[offset] << 8) | data[offset + 1];
            offset += 2;

            if (segmentLength < 2 || offset + segmentLength - 2 > data.Length)
            {
                throw new InvalidDataException("Invalid JPEG segment length.");
            }

            if (IsStartOfFrame(marker))
            {
                if (segmentLength < 8)
                {
                    throw new InvalidDataException("Invalid JPEG SOF segment.");
                }

                var bitsPerComponent = data[offset];
                var height = (data[offset + 1] << 8) | data[offset + 2];
                var width = (data[offset + 3] << 8) | data[offset + 4];
                var components = data[offset + 5];

                var colorSpaceName = components switch
                {
                    1 => "DeviceGray",
                    3 => "DeviceRGB",
                    4 => "DeviceCMYK",
                    _ => throw new NotSupportedException("JPEG component count is not supported.")
                };

                return new PdfImageData
                {
                    Width = width,
                    Height = height,
                    BitsPerComponent = bitsPerComponent,
                    ColorSpaceName = colorSpaceName,
                    FilterName = "DCTDecode",
                    Data = data
                };
            }

            offset += segmentLength - 2;
        }

        throw new InvalidDataException("JPEG SOF segment not found.");
    }

    private static bool IsPng(IReadOnlyList<byte> data)
    {
        return data.Count >= 8
            && data[0] == 0x89
            && data[1] == 0x50
            && data[2] == 0x4E
            && data[3] == 0x47
            && data[4] == 0x0D
            && data[5] == 0x0A
            && data[6] == 0x1A
            && data[7] == 0x0A;
    }

    private static bool IsJpeg(IReadOnlyList<byte> data)
    {
        return data.Count >= 2 && data[0] == 0xFF && data[1] == 0xD8;
    }

    private static bool IsStartOfFrame(byte marker)
    {
        return marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;
    }

    private static int ReadInt32BigEndian(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
    }

    private static void UnfilterPngRow(byte filterType, ReadOnlySpan<byte> filtered, ReadOnlySpan<byte> previousRow, Span<byte> destination, int bytesPerPixel)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            var left = i >= bytesPerPixel ? destination[i - bytesPerPixel] : (byte)0;
            var up = previousRow.Length > 0 ? previousRow[i] : (byte)0;
            var upperLeft = i >= bytesPerPixel && previousRow.Length > 0 ? previousRow[i - bytesPerPixel] : (byte)0;

            destination[i] = filterType switch
            {
                0 => filtered[i],
                1 => (byte)(filtered[i] + left),
                2 => (byte)(filtered[i] + up),
                3 => (byte)(filtered[i] + ((left + up) / 2)),
                4 => (byte)(filtered[i] + PaethPredictor(left, up, upperLeft)),
                _ => throw new InvalidDataException("Unsupported PNG filter type.")
            };
        }
    }

    private static byte PaethPredictor(byte left, byte up, byte upperLeft)
    {
        var p = left + up - upperLeft;
        var pa = Math.Abs(p - left);
        var pb = Math.Abs(p - up);
        var pc = Math.Abs(p - upperLeft);

        if (pa <= pb && pa <= pc)
        {
            return left;
        }

        return pb <= pc ? up : upperLeft;
    }

    private static byte[] DecompressZlib(byte[] data)
    {
        using var source = new MemoryStream(data);
        using var zlib = new ZLibStream(source, CompressionMode.Decompress);
        using var destination = new MemoryStream();
        zlib.CopyTo(destination);
        return destination.ToArray();
    }

    private static byte[] CompressZlib(byte[] data)
    {
        using var destination = new MemoryStream();
        using (var zlib = new ZLibStream(destination, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return destination.ToArray();
    }

    private static string ReadChunkType(byte[] data, int offset)
    {
        return string.Create(4, data.AsSpan(offset, 4), static (span, bytes) =>
        {
            span[0] = (char)bytes[0];
            span[1] = (char)bytes[1];
            span[2] = (char)bytes[2];
            span[3] = (char)bytes[3];
        });
    }
}
