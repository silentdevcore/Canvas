using System.IO.Compression;
using System.Text;
using PXA.Importer.Graphics;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Streams;
using SkiaSharp;

namespace PXA.Importer.Redaction;

/// <summary>
/// Irreversibly replaces covered image samples. Supported sources are decoded completely and
/// emitted as a new lossless Flate stream, so the original compressed payload is never copied
/// into the redacted resource.
/// </summary>
internal static class PdfImageRedactionEngine
{
    private const long MaximumPixels = 32_000_000;
    private const long MaximumSampleBytes = 64L * 1024 * 1024;

    public static bool CanRedactPartial(
        PdfObjectGraph graph,
        PdfDictionary resources,
        PdfImageElement image,
        out string reason)
    {
        if (!TryResolveStream(graph, resources, image, out var stream))
        {
            reason = "The image stream or resource reference could not be resolved.";
            return false;
        }

        if (Math.Abs(image.Transform.A * image.Transform.D - image.Transform.B * image.Transform.C) <= double.Epsilon)
        {
            reason = "The image uses a singular transform that cannot be mapped back to pixels.";
            return false;
        }

        return TryDecode(graph, stream, out _, out reason);
    }

    public static bool RequiresMaskReconstruction(
        PdfObjectGraph graph,
        PdfDictionary resources,
        PdfImageElement image) =>
        TryResolveStream(graph, resources, image, out var stream) &&
        (IsTruthy(Resolve(graph, stream.Dictionary["ImageMask"] ?? stream.Dictionary["IM"])) ||
         stream.Dictionary["Mask"] is not null ||
         stream.Dictionary["SMask"] is not null);

    public static bool TryCreateRedactedStream(
        PdfObjectGraph graph,
        PdfDictionary resources,
        PdfImageElement image,
        IReadOnlyList<PdfRectangle> regions,
        out PdfStreamObject redacted,
        out string reason)
    {
        redacted = null!;
        if (!TryResolveStream(graph, resources, image, out var source))
        {
            reason = "The image stream or resource reference could not be resolved.";
            return false;
        }

        if (!TryDecode(graph, source, out var pixels, out reason))
        {
            return false;
        }

        var touched = ZeroCoveredSamples(pixels, image.Transform, regions);
        if (touched == 0)
        {
            reason = "No image samples intersected the requested redaction regions.";
            return false;
        }

        var dictionary = new PdfDictionary(
            new Dictionary<string, PdfObject>(source.Dictionary.Values, StringComparer.Ordinal));
        dictionary["Type"] = new PdfName("XObject");
        dictionary["Subtype"] = new PdfName("Image");
        dictionary["Width"] = new PdfInteger(pixels.Width);
        dictionary["Height"] = new PdfInteger(pixels.Height);
        dictionary["BitsPerComponent"] = new PdfInteger(8);
        dictionary["ColorSpace"] = new PdfName(pixels.Components switch
        {
            1 => "DeviceGray",
            3 => "DeviceRGB",
            4 => "DeviceCMYK",
            _ => throw new InvalidOperationException("Unsupported redaction sample layout.")
        });
        dictionary["Filter"] = new PdfName("FlateDecode");
        foreach (var abbreviation in new[] { "W", "H", "BPC", "CS", "F", "DP", "D", "IM" })
        {
            dictionary[abbreviation] = null;
        }

        dictionary["DecodeParms"] = null;
        dictionary["Decode"] = null;
        dictionary["Length"] = null;
        dictionary["PXARedacted"] = new PdfBoolean(true);
        redacted = new PdfStreamObject(dictionary, Compress(pixels.Samples));
        reason = string.Empty;
        return true;
    }

    public static bool IsSecurelyRedacted(
        PdfObjectGraph graph,
        PdfDictionary resources,
        PdfImageElement image,
        IReadOnlyList<PdfRectangle> regions,
        out string reason)
    {
        if (!TryResolveStream(graph, resources, image, out var stream) ||
            !IsTruthy(Resolve(graph, stream.Dictionary["PXARedacted"])))
        {
            reason = "The image is not marked as a PXA pixel-redacted resource.";
            return false;
        }

        if (!TryDecode(graph, stream, out var pixels, out reason))
        {
            return false;
        }

        var inspected = 0;
        foreach (var region in regions)
        {
            if (!TryGetPixelRange(pixels, image.Transform, region, out var range))
            {
                continue;
            }

            for (var row = range.FirstRow; row <= range.LastRow; row++)
            {
                for (var column = range.FirstColumn; column <= range.LastColumn; column++)
                {
                    var offset = checked((row * pixels.Width + column) * pixels.Components);
                    if (pixels.Samples.AsSpan(offset, pixels.Components).IndexOfAnyExcept((byte)0) >= 0)
                    {
                        reason = "At least one covered image sample still contains non-zero data.";
                        return false;
                    }

                    inspected++;
                }
            }
        }

        reason = inspected > 0
            ? string.Empty
            : "No image samples could be mapped to the requested redaction regions.";
        return inspected > 0;
    }

    public static PdfStreamObject MakeInlineSafe(PdfStreamObject redacted)
    {
        var decoded = new PdfStreamDecoderRegistry(new PdfParseLimits
        {
            MaxDecodedStreamBytes = MaximumSampleBytes,
            MaxDecompressionRatio = 65_536
        }).Decode(redacted);
        var dictionary = new PdfDictionary(
            new Dictionary<string, PdfObject>(redacted.Dictionary.Values, StringComparer.Ordinal));
        dictionary["Filter"] = new PdfName("ASCIIHexDecode");
        dictionary["DecodeParms"] = null;
        var encoded = Encoding.ASCII.GetBytes(Convert.ToHexString(decoded.Span) + ">");
        return new PdfStreamObject(dictionary, encoded);
    }

    private static bool TryResolveStream(
        PdfObjectGraph graph,
        PdfDictionary resources,
        PdfImageElement image,
        out PdfStreamObject stream)
    {
        if (string.IsNullOrEmpty(image.ResourceName))
        {
            stream = image.SourceCommand.Operands.FirstOrDefault() as PdfStreamObject ?? null!;
            return stream is not null;
        }

        if (Resolve(graph, resources["XObject"]) is not PdfDictionary xObjects ||
            Resolve(graph, xObjects[image.ResourceName]) is not PdfStreamObject resolved)
        {
            stream = null!;
            return false;
        }

        stream = resolved;
        return true;
    }

    private static bool TryDecode(
        PdfObjectGraph graph,
        PdfStreamObject stream,
        out PixelBuffer pixels,
        out string reason)
    {
        pixels = default;
        if (IsTruthy(Resolve(graph, stream.Dictionary["ImageMask"] ?? stream.Dictionary["IM"])) ||
            stream.Dictionary["Mask"] is not null ||
            stream.Dictionary["SMask"] is not null)
        {
            reason = "Image masks and soft masks require mask-aware pixel reconstruction.";
            return false;
        }

        var filter = ReadSingleFilter(graph, stream.Dictionary["Filter"] ?? stream.Dictionary["F"]);
        if (filter is "JBIG2Decode")
        {
            reason = "JBIG2 image decoding is not available.";
            return false;
        }

        if (filter is "JPXDecode" or "DCTDecode")
        {
            return TryDecodeWithSkia(stream.EncodedBytes, filter, out pixels, out reason);
        }

        if (filter is not null and not "FlateDecode" and not "Fl" and
            not "ASCIIHexDecode" and not "AHx" and
            not "ASCII85Decode" and not "A85" and
            not "LZWDecode" and not "LZW" and not "RunLengthDecode" and not "RL")
        {
            reason = $"Image filter '{filter}' cannot be decoded for secure pixel redaction.";
            return false;
        }

        if (!TryReadPositiveInteger(graph, stream.Dictionary, "Width", "W", out var width) ||
            !TryReadPositiveInteger(graph, stream.Dictionary, "Height", "H", out var height))
        {
            reason = "The image has no valid width and height.";
            return false;
        }

        var bits = TryReadInteger(graph, stream.Dictionary["BitsPerComponent"] ?? stream.Dictionary["BPC"], out var bpc)
            ? bpc
            : 8;
        if (bits != 8)
        {
            reason = $"Only 8-bit image samples are currently redacted; this image uses {bits} bits.";
            return false;
        }

        var components = ResolveComponents(graph, stream.Dictionary["ColorSpace"] ?? stream.Dictionary["CS"]);
        if (components is not (1 or 3 or 4))
        {
            reason = "Only DeviceGray, DeviceRGB, and DeviceCMYK images are currently redacted.";
            return false;
        }

        if (!TryValidateSize(width, height, components, out var byteCount, out reason))
        {
            return false;
        }

        ReadOnlyMemory<byte> decoded;
        try
        {
            decoded = filter is null
                ? stream.EncodedBytes
                : new PdfStreamDecoderRegistry(new PdfParseLimits
                {
                    MaxDecodedStreamBytes = MaximumSampleBytes,
                    MaxDecompressionRatio = IsTruthy(Resolve(graph, stream.Dictionary["PXARedacted"]))
                        ? 65_536
                        : new PdfParseLimits().MaxDecompressionRatio
                }).Decode(NormalizeInlineDictionary(stream));
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or PdfParseLimitExceededException)
        {
            reason = $"The image stream could not be decoded safely: {exception.Message}";
            return false;
        }

        if (decoded.Length != byteCount)
        {
            reason = $"The decoded image contains {decoded.Length} bytes; exactly {byteCount} were expected.";
            return false;
        }

        pixels = new PixelBuffer(width, height, components, decoded.ToArray());
        reason = string.Empty;
        return true;
    }

    private static bool TryDecodeWithSkia(
        ReadOnlyMemory<byte> encoded,
        string filter,
        out PixelBuffer pixels,
        out string reason)
    {
        pixels = default;
        using var data = SKData.CreateCopy(encoded.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            reason = $"The local image codec cannot decode {filter}.";
            return false;
        }

        var info = codec.Info;
        if (!TryValidateSize(info.Width, info.Height, 3, out var byteCount, out reason))
        {
            return false;
        }

        var rgbaInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(rgbaInfo);
        var result = codec.GetPixels(rgbaInfo, bitmap.GetPixels());
        if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
        {
            reason = $"The local image codec returned {result} for {filter}.";
            return false;
        }

        var samples = new byte[byteCount];
        var offset = 0;
        for (var row = 0; row < info.Height; row++)
        {
            for (var column = 0; column < info.Width; column++)
            {
                var color = bitmap.GetPixel(column, row);
                samples[offset++] = color.Red;
                samples[offset++] = color.Green;
                samples[offset++] = color.Blue;
            }
        }

        pixels = new PixelBuffer(info.Width, info.Height, 3, samples);
        reason = string.Empty;
        return true;
    }

    private static int ZeroCoveredSamples(
        PixelBuffer pixels,
        PdfMatrix transform,
        IReadOnlyList<PdfRectangle> regions)
    {
        var touched = 0;
        foreach (var region in regions)
        {
            if (!TryGetPixelRange(pixels, transform, region, out var range))
            {
                continue;
            }

            for (var row = range.FirstRow; row <= range.LastRow; row++)
            {
                for (var column = range.FirstColumn; column <= range.LastColumn; column++)
                {
                    var offset = checked((row * pixels.Width + column) * pixels.Components);
                    pixels.Samples.AsSpan(offset, pixels.Components).Clear();
                    touched++;
                }
            }
        }

        return touched;
    }

    private static bool TryGetPixelRange(
        PixelBuffer pixels,
        PdfMatrix transform,
        PdfRectangle region,
        out PixelRange range)
    {
        range = default;
        if (Math.Abs(transform.A * transform.D - transform.B * transform.C) <= double.Epsilon)
        {
            return false;
        }

        var local = new[]
        {
            MatrixEngine.WorldToLocal(new PdfPoint(region.Left, region.Bottom), transform),
            MatrixEngine.WorldToLocal(new PdfPoint(region.Right, region.Bottom), transform),
            MatrixEngine.WorldToLocal(new PdfPoint(region.Right, region.Top), transform),
            MatrixEngine.WorldToLocal(new PdfPoint(region.Left, region.Top), transform)
        };
        var left = Math.Clamp(local.Min(static point => point.X), 0, 1);
        var right = Math.Clamp(local.Max(static point => point.X), 0, 1);
        var bottom = Math.Clamp(local.Min(static point => point.Y), 0, 1);
        var top = Math.Clamp(local.Max(static point => point.Y), 0, 1);
        if (right <= left || top <= bottom)
        {
            return false;
        }

        range = new PixelRange(
            Math.Clamp((int)Math.Floor(left * pixels.Width), 0, pixels.Width - 1),
            Math.Clamp((int)Math.Ceiling(right * pixels.Width) - 1, 0, pixels.Width - 1),
            Math.Clamp((int)Math.Floor((1 - top) * pixels.Height), 0, pixels.Height - 1),
            Math.Clamp((int)Math.Ceiling((1 - bottom) * pixels.Height) - 1, 0, pixels.Height - 1));
        return true;
    }

    private static bool TryValidateSize(
        int width,
        int height,
        int components,
        out int byteCount,
        out string reason)
    {
        byteCount = 0;
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPixels)
        {
            reason = $"The image dimensions exceed the {MaximumPixels} pixel redaction limit.";
            return false;
        }

        var length = (long)width * height * components;
        if (length > MaximumSampleBytes || length > int.MaxValue)
        {
            reason = $"The decoded image exceeds the {MaximumSampleBytes} byte redaction limit.";
            return false;
        }

        byteCount = (int)length;
        reason = string.Empty;
        return true;
    }

    private static ReadOnlyMemory<byte> Compress(ReadOnlySpan<byte> samples)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(samples);
        }

        return output.ToArray();
    }

    private static PdfStreamObject NormalizeInlineDictionary(PdfStreamObject stream)
    {
        if (stream.Dictionary["Filter"] is not null || stream.Dictionary["F"] is not { } abbreviatedFilter)
        {
            return stream;
        }

        var dictionary = new PdfDictionary(
            new Dictionary<string, PdfObject>(stream.Dictionary.Values, StringComparer.Ordinal));
        dictionary["Filter"] = abbreviatedFilter;
        if (dictionary["DecodeParms"] is null && dictionary["DP"] is { } abbreviatedParameters)
        {
            dictionary["DecodeParms"] = abbreviatedParameters;
        }

        return new PdfStreamObject(dictionary, stream.EncodedBytes);
    }

    private static string? ReadSingleFilter(PdfObjectGraph graph, PdfObject? value)
    {
        var resolved = Resolve(graph, value);
        return resolved switch
        {
            null => null,
            PdfName name => name.Value,
            PdfArray { Items.Count: 1 } array when Resolve(graph, array.Items[0]) is PdfName name => name.Value,
            _ => "multiple-or-invalid"
        };
    }

    private static int ResolveComponents(PdfObjectGraph graph, PdfObject? value)
    {
        var resolved = Resolve(graph, value);
        return resolved switch
        {
            PdfName { Value: "DeviceGray" or "G" } => 1,
            PdfName { Value: "DeviceRGB" or "RGB" } => 3,
            PdfName { Value: "DeviceCMYK" or "CMYK" } => 4,
            _ => 0
        };
    }

    private static bool TryReadPositiveInteger(
        PdfObjectGraph graph,
        PdfDictionary dictionary,
        string key,
        string abbreviation,
        out int value) =>
        TryReadInteger(graph, dictionary[key] ?? dictionary[abbreviation], out value) && value > 0;

    private static bool TryReadInteger(PdfObjectGraph graph, PdfObject? value, out int result)
    {
        if (Resolve(graph, value) is PdfInteger integer && integer.Value is >= int.MinValue and <= int.MaxValue)
        {
            result = (int)integer.Value;
            return true;
        }

        result = 0;
        return false;
    }

    private static bool IsTruthy(PdfObject? value) => value switch
    {
        PdfBoolean boolean => boolean.Value,
        // Content-stream operands currently retain boolean keywords as names. Pixel
        // verification below remains authoritative, so accepting this spelling is safe.
        PdfName { Value: "true" } => true,
        _ => false
    };

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    }

    private readonly record struct PixelBuffer(int Width, int Height, int Components, byte[] Samples);

    private readonly record struct PixelRange(int FirstColumn, int LastColumn, int FirstRow, int LastRow);
}
