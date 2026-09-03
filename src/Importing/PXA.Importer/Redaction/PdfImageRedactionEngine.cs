using System.IO.Compression;
using System.Text;
using PXA.Importer.Color;
using PXA.Importer.Graphics;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Streams;
using SkiaSharp;

namespace PXA.Importer.Redaction;

internal sealed record PdfRedactedImage(
    PdfStreamObject Image,
    IReadOnlyList<PdfRedactedImageMask> Masks);

internal sealed record PdfRedactedImageMask(string DictionaryKey, PdfStreamObject Stream);

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

        if (string.IsNullOrEmpty(image.ResourceName) && EnumerateMaskStreams(graph, stream).Any())
        {
            reason = "Inline images with external mask streams cannot be rewritten atomically.";
            return false;
        }

        if (!TryDecode(graph, resources, stream, ignoreAttachedMasks: true, out _, out reason))
        {
            return false;
        }

        foreach (var mask in EnumerateMaskStreams(graph, stream))
        {
            if (!TryDecode(graph, resources, mask.Stream, ignoreAttachedMasks: false, out _, out reason))
            {
                return false;
            }
        }

        reason = string.Empty;
        return true;
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
        out PdfRedactedImage redacted,
        out string reason)
    {
        redacted = null!;
        if (!TryResolveStream(graph, resources, image, out var source))
        {
            reason = "The image stream or resource reference could not be resolved.";
            return false;
        }

        if (!TryDecode(graph, resources, source, ignoreAttachedMasks: true, out var pixels, out reason))
        {
            return false;
        }

        var touched = ZeroCoveredSamples(pixels, image.Transform, regions);
        if (touched == 0)
        {
            reason = "No image samples intersected the requested redaction regions.";
            return false;
        }

        var imageStream = CreateRedactedStream(source, pixels);
        var masks = new List<PdfRedactedImageMask>();
        foreach (var mask in EnumerateMaskStreams(graph, source))
        {
            if (!TryDecode(graph, resources, mask.Stream, ignoreAttachedMasks: false, out var maskPixels, out reason))
            {
                return false;
            }

            if (ZeroCoveredSamples(maskPixels, image.Transform, regions) == 0)
            {
                reason = $"No {mask.DictionaryKey} samples intersected the requested redaction regions.";
                return false;
            }

            masks.Add(new PdfRedactedImageMask(mask.DictionaryKey, CreateRedactedStream(mask.Stream, maskPixels)));
        }

        redacted = new PdfRedactedImage(imageStream, masks);
        reason = string.Empty;
        return true;
    }

    private static PdfStreamObject CreateRedactedStream(PdfStreamObject source, PixelBuffer pixels)
    {
        var dictionary = new PdfDictionary(
            new Dictionary<string, PdfObject>(source.Dictionary.Values, StringComparer.Ordinal));
        dictionary["Type"] = new PdfName("XObject");
        dictionary["Subtype"] = new PdfName("Image");
        dictionary["Width"] = new PdfInteger(pixels.Width);
        dictionary["Height"] = new PdfInteger(pixels.Height);
        dictionary["BitsPerComponent"] = new PdfInteger(pixels.BitsPerComponent);
        if (pixels.StandardizedToRgb)
        {
            dictionary["ColorSpace"] = new PdfName("DeviceRGB");
            dictionary["Decode"] = null;
            dictionary["Mask"] = null;
            dictionary["SMask"] = null;
        }
        else if ((source.Dictionary["ColorSpace"] ?? source.Dictionary["CS"]) is { } colorSpace)
        {
            dictionary["ColorSpace"] = colorSpace;
        }
        dictionary["Filter"] = new PdfName("FlateDecode");
        foreach (var abbreviation in new[] { "W", "H", "BPC", "CS", "F", "DP", "D", "IM" })
        {
            dictionary[abbreviation] = null;
        }

        dictionary["DecodeParms"] = null;
        dictionary["Length"] = null;
        dictionary["PXARedacted"] = new PdfBoolean(true);
        return new PdfStreamObject(dictionary, Compress(pixels.Samples));
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

        if (!TryDecode(graph, resources, stream, ignoreAttachedMasks: true, out var pixels, out reason) ||
            !CoveredPixelsAreZero(pixels, image.Transform, regions, out reason))
        {
            return false;
        }

        foreach (var mask in EnumerateMaskStreams(graph, stream))
        {
            if (!IsTruthy(Resolve(graph, mask.Stream.Dictionary["PXARedacted"])) ||
                !TryDecode(graph, resources, mask.Stream, ignoreAttachedMasks: false, out var maskPixels, out reason) ||
                !CoveredPixelsAreZero(maskPixels, image.Transform, regions, out reason))
            {
                reason = $"The {mask.DictionaryKey} stream is not securely pixel-redacted. {reason}";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool CoveredPixelsAreZero(
        PixelBuffer pixels,
        PdfMatrix transform,
        IReadOnlyList<PdfRectangle> regions,
        out string reason)
    {
        var inspected = 0;
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
                    if (!IsPixelZero(pixels, row, column))
                    {
                        reason = "At least one covered image sample still contains non-zero data.";
                        return false;
                    }

                    inspected++;
                }
            }
        }

        if (inspected == 0)
        {
            reason = "No image samples could be mapped to the requested redaction regions.";
            return false;
        }

        reason = string.Empty;
        return true;
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
        PdfDictionary resources,
        PdfStreamObject stream,
        bool ignoreAttachedMasks,
        out PixelBuffer pixels,
        out string reason)
    {
        pixels = default;
        var isImageMask = IsTruthy(Resolve(graph, stream.Dictionary["ImageMask"] ?? stream.Dictionary["IM"]));
        var mask = Resolve(graph, stream.Dictionary["Mask"]);
        if (ignoreAttachedMasks && !HasValidAttachedMaskEntries(graph, stream, out reason))
        {
            return false;
        }

        if (!ignoreAttachedMasks && ((mask is not null && mask is not PdfArray) || stream.Dictionary["SMask"] is not null))
        {
            reason = "Explicit mask streams and soft masks require coupled pixel reconstruction.";
            return false;
        }

        var filter = ReadSingleFilter(graph, stream.Dictionary["Filter"] ?? stream.Dictionary["F"]);
        if (filter is "JPXDecode" or "DCTDecode" && mask is PdfArray)
        {
            reason = $"{filter} images with a color-key mask require mask-aware color reconstruction.";
            return false;
        }

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
            : isImageMask ? 1 : 8;
        if (bits is not (1 or 2 or 4 or 8 or 16))
        {
            reason = $"Image samples with {bits} bits per component cannot be redacted deterministically.";
            return false;
        }

        var components = isImageMask
            ? 1
            : ResolveComponents(graph, resources, stream.Dictionary["ColorSpace"] ?? stream.Dictionary["CS"]);
        if (components is < 1 or > 32)
        {
            reason = "The image color space does not expose a bounded component count.";
            return false;
        }

        if (!TryValidatePackedSize(width, height, components, bits, out var byteCount, out reason))
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

        pixels = new PixelBuffer(width, height, components, bits, decoded.ToArray(), StandardizedToRgb: false);
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

        pixels = new PixelBuffer(info.Width, info.Height, 3, 8, samples, StandardizedToRgb: true);
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
                    ZeroPixel(pixels, row, column);
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

    private static bool TryValidatePackedSize(
        int width,
        int height,
        int components,
        int bitsPerComponent,
        out int byteCount,
        out string reason)
    {
        byteCount = 0;
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPixels)
        {
            reason = $"The image dimensions exceed the {MaximumPixels} pixel redaction limit.";
            return false;
        }

        var rowBits = checked((long)width * components * bitsPerComponent);
        var length = checked(((rowBits + 7) / 8) * height);
        if (length > MaximumSampleBytes || length > int.MaxValue)
        {
            reason = $"The decoded image exceeds the {MaximumSampleBytes} byte redaction limit.";
            return false;
        }

        byteCount = (int)length;
        reason = string.Empty;
        return true;
    }

    private static void ZeroPixel(PixelBuffer pixels, int row, int column)
    {
        var rowBits = checked(pixels.Width * pixels.Components * pixels.BitsPerComponent);
        var rowOffset = checked(row * ((rowBits + 7) / 8));
        var firstBit = checked(column * pixels.Components * pixels.BitsPerComponent);
        var bitCount = checked(pixels.Components * pixels.BitsPerComponent);
        for (var bit = 0; bit < bitCount; bit++)
        {
            var absolute = checked(rowOffset * 8 + firstBit + bit);
            pixels.Samples[absolute / 8] &= (byte)~(1 << (7 - absolute % 8));
        }
    }

    private static bool IsPixelZero(PixelBuffer pixels, int row, int column)
    {
        var rowBits = checked(pixels.Width * pixels.Components * pixels.BitsPerComponent);
        var rowOffset = checked(row * ((rowBits + 7) / 8));
        var firstBit = checked(column * pixels.Components * pixels.BitsPerComponent);
        var bitCount = checked(pixels.Components * pixels.BitsPerComponent);
        for (var bit = 0; bit < bitCount; bit++)
        {
            var absolute = checked(rowOffset * 8 + firstBit + bit);
            if ((pixels.Samples[absolute / 8] & (1 << (7 - absolute % 8))) != 0)
            {
                return false;
            }
        }

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

    private static int ResolveComponents(PdfObjectGraph graph, PdfDictionary resources, PdfObject? value)
    {
        var resolved = Resolve(graph, value);
        if (resolved is PdfName name &&
            name.Value is not ("DeviceGray" or "G" or "DeviceRGB" or "RGB" or "DeviceCMYK" or "CMYK") &&
            Resolve(graph, resources["ColorSpace"]) is PdfDictionary colorSpaces &&
            colorSpaces[name.Value] is { } named)
        {
            resolved = Resolve(graph, named);
        }

        return PdfSrgbColorConverter.Resolve(resolved, valueToResolve => Resolve(graph, valueToResolve)).ComponentCount;
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

    private static IEnumerable<PdfRedactedImageMask> EnumerateMaskStreams(
        PdfObjectGraph graph,
        PdfStreamObject image)
    {
        foreach (var key in new[] { "SMask", "Mask" })
        {
            if (Resolve(graph, image.Dictionary[key]) is PdfStreamObject stream)
            {
                yield return new PdfRedactedImageMask(key, stream);
            }
        }
    }

    private static bool HasValidAttachedMaskEntries(
        PdfObjectGraph graph,
        PdfStreamObject image,
        out string reason)
    {
        if (image.Dictionary["SMask"] is { } softMask && Resolve(graph, softMask) is not PdfStreamObject)
        {
            reason = "The soft-mask entry does not resolve to an image stream.";
            return false;
        }

        if (image.Dictionary["Mask"] is { } mask && Resolve(graph, mask) is not (PdfStreamObject or PdfArray))
        {
            reason = "The mask entry is neither an image stream nor a color-key array.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    }

    private readonly record struct PixelBuffer(
        int Width,
        int Height,
        int Components,
        int BitsPerComponent,
        byte[] Samples,
        bool StandardizedToRgb);

    private readonly record struct PixelRange(int FirstColumn, int LastColumn, int FirstRow, int LastRow);
}
