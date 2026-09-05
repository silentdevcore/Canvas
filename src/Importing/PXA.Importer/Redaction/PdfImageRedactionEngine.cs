using System.IO.Compression;
using System.Text;
using CoreJ2K;
using JBig2Decoder.NETStandard;
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
    private const int MaximumEncodedCodecBytes = 16 * 1024 * 1024;
    private const uint MaximumJbig2Symbols = 100_000;
    private const uint MaximumJbig2SymbolInstances = 100_000;

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

        if (!TryReadPositiveInteger(graph, stream.Dictionary, "Width", "W", out var width) ||
            !TryReadPositiveInteger(graph, stream.Dictionary, "Height", "H", out var height))
        {
            reason = "The image has no valid width and height.";
            return false;
        }

        var filter = ReadSingleFilter(graph, stream.Dictionary["Filter"] ?? stream.Dictionary["F"]);
        if (filter is ("JPXDecode" or "DCTDecode" or "JBIG2Decode") && mask is PdfArray)
        {
            reason = $"{filter} images with a color-key mask require mask-aware color reconstruction.";
            return false;
        }

        if (filter is "JBIG2Decode")
        {
            return TryDecodeJbig2(graph, stream, width, height, out pixels, out reason);
        }

        if (filter is "JPXDecode")
        {
            return TryDecodeJpx(stream.EncodedBytes, width, height, out pixels, out reason);
        }

        if (filter is "DCTDecode")
        {
            return TryDecodeWithSkia(stream.EncodedBytes, filter, width, height, out pixels, out reason);
        }

        if (filter is not null and not "FlateDecode" and not "Fl" and
            not "ASCIIHexDecode" and not "AHx" and
            not "ASCII85Decode" and not "A85" and
            not "LZWDecode" and not "LZW" and not "RunLengthDecode" and not "RL")
        {
            reason = $"Image filter '{filter}' cannot be decoded for secure pixel redaction.";
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
        int expectedWidth,
        int expectedHeight,
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
        if (info.Width != expectedWidth || info.Height != expectedHeight)
        {
            reason = $"The {filter} dimensions {info.Width}x{info.Height} do not match the PDF image dictionary {expectedWidth}x{expectedHeight}.";
            return false;
        }

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

    private static bool TryDecodeJpx(
        ReadOnlyMemory<byte> encoded,
        int expectedWidth,
        int expectedHeight,
        out PixelBuffer pixels,
        out string reason)
    {
        pixels = default;
        if (encoded.Length is 0 or > MaximumEncodedCodecBytes)
        {
            reason = $"The JPX stream exceeds the {MaximumEncodedCodecBytes} byte codec-input limit.";
            return false;
        }
        if (!TryReadJpxDimensions(encoded.Span, out var encodedWidth, out var encodedHeight, out reason) ||
            encodedWidth != expectedWidth || encodedHeight != expectedHeight)
        {
            if (string.IsNullOrEmpty(reason))
            {
                reason = $"The JPX dimensions {encodedWidth}x{encodedHeight} do not match the PDF image dictionary {expectedWidth}x{expectedHeight}.";
            }
            return false;
        }
        if (!TryValidateSize(expectedWidth, expectedHeight, 3, out var byteCount, out reason))
        {
            return false;
        }

        try
        {
            using var image = J2kImage.FromBytes(encoded);
            if (image.Width != expectedWidth || image.Height != expectedHeight)
            {
                reason = $"The JPX dimensions {image.Width}x{image.Height} do not match the PDF image dictionary {expectedWidth}x{expectedHeight}.";
                return false;
            }

            if (image.NumberOfComponents is not (1 or 3))
            {
                reason = $"The JPX image has {image.NumberOfComponents} components; secure redaction currently requires grayscale or RGB samples.";
                return false;
            }

            var components = Enumerable.Range(0, image.NumberOfComponents)
                .Select(image.GetComponentBytes)
                .ToArray();
            var pixelCount = checked(expectedWidth * expectedHeight);
            if (components.Any(component => component.Length != pixelCount))
            {
                reason = "The JPX decoder returned an unexpected component length.";
                return false;
            }

            var samples = new byte[byteCount];
            for (var index = 0; index < pixelCount; index++)
            {
                if (components.Length == 1)
                {
                    samples[index * 3] = components[0][index];
                    samples[index * 3 + 1] = components[0][index];
                    samples[index * 3 + 2] = components[0][index];
                }
                else
                {
                    samples[index * 3] = components[0][index];
                    samples[index * 3 + 1] = components[1][index];
                    samples[index * 3 + 2] = components[2][index];
                }
            }

            pixels = new PixelBuffer(expectedWidth, expectedHeight, 3, 8, samples, StandardizedToRgb: true);
            reason = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            reason = $"The bundled JPEG-2000 codec could not decode JPX safely: {exception.Message}";
            return false;
        }
    }

    private static bool TryReadJpxDimensions(
        ReadOnlySpan<byte> data,
        out int width,
        out int height,
        out string reason)
    {
        width = 0;
        height = 0;
        var codestream = data;
        if (data.Length >= 12 && data[4..8].SequenceEqual("jP  "u8))
        {
            var offset = 0;
            codestream = default;
            while (data.Length - offset >= 8)
            {
                var boxLength = ReadUInt32BigEndian(data[offset..]);
                var type = data.Slice(offset + 4, 4);
                var headerLength = 8;
                ulong resolvedLength = boxLength;
                if (boxLength == 1)
                {
                    if (data.Length - offset < 16)
                    {
                        reason = "The JPX extended box header is truncated.";
                        return false;
                    }
                    resolvedLength = ReadUInt64BigEndian(data[(offset + 8)..]);
                    headerLength = 16;
                }
                else if (boxLength == 0)
                {
                    resolvedLength = (ulong)(data.Length - offset);
                }
                if (resolvedLength < (ulong)headerLength || resolvedLength > (ulong)(data.Length - offset))
                {
                    reason = "The JPX box length exceeds the stream boundary.";
                    return false;
                }
                if (type.SequenceEqual("jp2c"u8))
                {
                    codestream = data.Slice(offset + headerLength, (int)resolvedLength - headerLength);
                    break;
                }
                offset += (int)resolvedLength;
            }
            if (codestream.IsEmpty)
            {
                reason = "The JPX container contains no JPEG-2000 codestream box.";
                return false;
            }
        }

        if (codestream.Length < 4 || codestream[0] != 0xFF || codestream[1] != 0x4F)
        {
            reason = "The JPX payload has no valid JPEG-2000 start-of-codestream marker.";
            return false;
        }
        var markerOffset = 2;
        while (codestream.Length - markerOffset >= 4)
        {
            if (codestream[markerOffset] != 0xFF)
            {
                reason = "The JPX codestream contains an invalid marker boundary.";
                return false;
            }
            var marker = codestream[markerOffset + 1];
            markerOffset += 2;
            if (marker == 0x51)
            {
                if (codestream.Length - markerOffset < 38)
                {
                    reason = "The JPX size marker is truncated.";
                    return false;
                }
                var segmentLength = ReadUInt16BigEndian(codestream[markerOffset..]);
                if (segmentLength < 38 || segmentLength > codestream.Length - markerOffset)
                {
                    reason = "The JPX size marker has an invalid length.";
                    return false;
                }
                var xSize = ReadUInt32BigEndian(codestream[(markerOffset + 4)..]);
                var ySize = ReadUInt32BigEndian(codestream[(markerOffset + 8)..]);
                var xOrigin = ReadUInt32BigEndian(codestream[(markerOffset + 12)..]);
                var yOrigin = ReadUInt32BigEndian(codestream[(markerOffset + 16)..]);
                var tileWidth = ReadUInt32BigEndian(codestream[(markerOffset + 20)..]);
                var tileHeight = ReadUInt32BigEndian(codestream[(markerOffset + 24)..]);
                var components = ReadUInt16BigEndian(codestream[(markerOffset + 36)..]);
                if (xSize <= xOrigin || ySize <= yOrigin ||
                    xSize - xOrigin > int.MaxValue || ySize - yOrigin > int.MaxValue)
                {
                    reason = "The JPX size marker contains invalid dimensions.";
                    return false;
                }
                if (components is not (1 or 3) || segmentLength < 38 + (3 * components))
                {
                    reason = "The JPX size marker must describe complete grayscale or RGB components.";
                    return false;
                }
                if (tileWidth == 0 || tileHeight == 0 || (ulong)tileWidth * tileHeight > MaximumPixels)
                {
                    reason = $"A JPX tile exceeds the {MaximumPixels} pixel redaction limit.";
                    return false;
                }
                width = (int)(xSize - xOrigin);
                height = (int)(ySize - yOrigin);
                reason = string.Empty;
                return true;
            }
            if (marker is 0x93 or 0xD9)
            {
                break;
            }
            var length = ReadUInt16BigEndian(codestream[markerOffset..]);
            if (length < 2 || length > codestream.Length - markerOffset)
            {
                reason = "The JPX marker segment exceeds the codestream boundary.";
                return false;
            }
            markerOffset += length;
        }

        reason = "The JPX codestream contains no size marker.";
        return false;
    }

    private static bool TryDecodeJbig2(
        PdfObjectGraph graph,
        PdfStreamObject stream,
        int expectedWidth,
        int expectedHeight,
        out PixelBuffer pixels,
        out string reason)
    {
        pixels = default;
        if (stream.EncodedBytes.Length is 0 or > MaximumEncodedCodecBytes)
        {
            reason = $"The JBIG2 stream exceeds the {MaximumEncodedCodecBytes} byte codec-input limit.";
            return false;
        }

        if (!TryReadJbig2PageDimensions(stream.EncodedBytes.Span, out var encodedWidth, out var encodedHeight, out reason) ||
            encodedWidth != expectedWidth || encodedHeight != expectedHeight)
        {
            if (string.IsNullOrEmpty(reason))
            {
                reason = $"The JBIG2 dimensions {encodedWidth}x{encodedHeight} do not match the PDF image dictionary {expectedWidth}x{expectedHeight}.";
            }
            return false;
        }

        if (!TryValidateSize(expectedWidth, expectedHeight, 3, out var byteCount, out reason))
        {
            return false;
        }

        try
        {
            var decoder = new JBIG2StreamDecoder { TolerateMissingSegments = false };
            if (!TryReadJbig2Globals(graph, stream, out var globals, out reason))
            {
                return false;
            }
            if (!globals.IsEmpty)
            {
                decoder.SetGlobalData(globals.ToArray());
            }

            var samples = decoder.DecodeJBIG2(stream.EncodedBytes.ToArray(), out var width, out var height);
            if (width != expectedWidth || height != expectedHeight || samples.Length != byteCount)
            {
                reason = "The JBIG2 decoder result does not match the validated PDF image dimensions.";
                return false;
            }

            pixels = new PixelBuffer(width, height, 3, 8, samples, StandardizedToRgb: true);
            reason = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            reason = $"The bundled JBIG2 codec could not decode the image safely: {exception.Message}";
            return false;
        }
    }

    private static bool TryReadJbig2Globals(
        PdfObjectGraph graph,
        PdfStreamObject stream,
        out ReadOnlyMemory<byte> globals,
        out string reason)
    {
        globals = ReadOnlyMemory<byte>.Empty;
        var parameters = Resolve(graph, stream.Dictionary["DecodeParms"] ?? stream.Dictionary["DP"]);
        if (parameters is PdfArray { Items.Count: 1 } array)
        {
            parameters = Resolve(graph, array.Items[0]);
        }
        if (parameters is null)
        {
            reason = string.Empty;
            return true;
        }
        if (parameters is not PdfDictionary dictionary)
        {
            reason = "The JBIG2 DecodeParms entry is not a dictionary.";
            return false;
        }
        if (dictionary["JBIG2Globals"] is null)
        {
            reason = string.Empty;
            return true;
        }
        if (Resolve(graph, dictionary["JBIG2Globals"]) is not PdfStreamObject globalStream)
        {
            reason = "The JBIG2Globals entry does not resolve to a stream.";
            return false;
        }

        try
        {
            globals = globalStream.Dictionary["Filter"] is null
                ? globalStream.EncodedBytes
                : new PdfStreamDecoderRegistry(new PdfParseLimits
                {
                    MaxDecodedStreamBytes = MaximumEncodedCodecBytes,
                    MaxDecompressionRatio = 1_024
                }).Decode(globalStream);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or PdfParseLimitExceededException)
        {
            reason = $"The JBIG2Globals stream could not be decoded safely: {exception.Message}";
            return false;
        }

        if (globals.Length > MaximumEncodedCodecBytes)
        {
            reason = $"The JBIG2Globals stream exceeds the {MaximumEncodedCodecBytes} byte codec-input limit.";
            return false;
        }
        if (!globals.IsEmpty && !TryValidateJbig2SegmentBoundaries(globals.Span, out reason))
        {
            reason = $"The JBIG2Globals stream is malformed: {reason}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryReadJbig2PageDimensions(
        ReadOnlySpan<byte> data,
        out int width,
        out int height,
        out string reason)
    {
        width = 0;
        height = 0;
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            reason = "A PDF JBIG2 stream must use embedded organization without a standalone file header.";
            return false;
        }

        var offset = 0;
        var foundPageInformation = false;
        while (offset < data.Length)
        {
            if (!TryReadJbig2Segment(data, ref offset, out var type, out var payload, out reason))
            {
                return false;
            }
            if (!TryValidateJbig2SegmentPayload(type, payload, out var payloadReason))
            {
                reason = payloadReason;
                return false;
            }
            if (type != 48)
            {
                continue;
            }
            if (foundPageInformation)
            {
                reason = "The JBIG2 stream contains multiple page-information segments.";
                return false;
            }
            if (payload.Length < 8)
            {
                reason = "The JBIG2 page-information segment is truncated.";
                return false;
            }

            var parsedWidth = ReadUInt32BigEndian(payload);
            var parsedHeight = ReadUInt32BigEndian(payload[4..]);
            if (parsedWidth is 0 or > int.MaxValue || parsedHeight is 0 or > int.MaxValue ||
                (ulong)parsedWidth * parsedHeight > MaximumPixels)
            {
                reason = $"The JBIG2 page-information segment exceeds the {MaximumPixels} pixel redaction limit.";
                return false;
            }
            width = (int)parsedWidth;
            height = (int)parsedHeight;
            foundPageInformation = true;
        }

        reason = foundPageInformation ? string.Empty : "The JBIG2 stream contains no page-information segment.";
        return foundPageInformation;
    }

    private static bool TryValidateJbig2SegmentBoundaries(ReadOnlySpan<byte> data, out string reason)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            if (!TryReadJbig2Segment(data, ref offset, out var type, out var payload, out reason))
            {
                return false;
            }
            if (!TryValidateJbig2SegmentPayload(type, payload, out reason))
            {
                return false;
            }
        }
        reason = string.Empty;
        return true;
    }

    private static bool IsJbig2RegionSegment(int type) => type is 4 or 6 or 7 or 20 or 22 or 23 or 36 or 38 or 39 or 40 or 42 or 43;

    private static bool TryValidateJbig2SegmentPayload(int type, ReadOnlySpan<byte> payload, out string reason)
    {
        if (IsJbig2RegionSegment(type) && !TryReadBoundedJbig2Region(payload, out reason))
        {
            return false;
        }
        if (type == 0 && !TryReadBoundedJbig2SymbolDictionary(payload, out reason))
        {
            return false;
        }
        if (type == 16 && !TryReadBoundedJbig2PatternDictionary(payload, out reason))
        {
            return false;
        }
        if (type is 4 or 6 or 7 && !TryReadBoundedJbig2TextRegion(payload, out reason))
        {
            return false;
        }
        if (type is 20 or 22 or 23 && !TryReadBoundedJbig2HalftoneGrid(payload, out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryReadBoundedJbig2Region(ReadOnlySpan<byte> payload, out string reason)
    {
        if (payload.Length < 8)
        {
            reason = "A JBIG2 region segment is truncated.";
            return false;
        }
        var width = ReadUInt32BigEndian(payload);
        var height = ReadUInt32BigEndian(payload[4..]);
        if (width is 0 or > int.MaxValue || height is 0 or > int.MaxValue ||
            (ulong)width * height > MaximumPixels)
        {
            reason = $"A JBIG2 region exceeds the {MaximumPixels} pixel redaction limit.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool TryReadBoundedJbig2SymbolDictionary(ReadOnlySpan<byte> payload, out string reason)
    {
        if (payload.Length < 10)
        {
            reason = "A JBIG2 symbol-dictionary segment is truncated.";
            return false;
        }

        var flags = ReadUInt16BigEndian(payload);
        var usesHuffman = (flags & 0x0001) != 0;
        var usesRefinementAggregation = (flags & 0x0002) != 0;
        var template = (flags >> 10) & 0x0003;
        var refinementTemplate = (flags >> 12) & 0x0001;
        var countOffset = 2;
        if (!usesHuffman)
        {
            countOffset += template == 0 ? 8 : 2;
        }
        if (usesRefinementAggregation && refinementTemplate == 0)
        {
            countOffset += 4;
        }
        if (payload.Length - countOffset < 8)
        {
            reason = "A JBIG2 symbol-dictionary segment has truncated adaptive-template or symbol-count fields.";
            return false;
        }

        var exportedSymbols = ReadUInt32BigEndian(payload[countOffset..]);
        var newSymbols = ReadUInt32BigEndian(payload[(countOffset + 4)..]);
        if (exportedSymbols > MaximumJbig2Symbols || newSymbols > MaximumJbig2Symbols ||
            (ulong)exportedSymbols + newSymbols > MaximumJbig2Symbols)
        {
            reason = $"A JBIG2 symbol dictionary exceeds the {MaximumJbig2Symbols} symbol redaction limit.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryReadBoundedJbig2PatternDictionary(ReadOnlySpan<byte> payload, out string reason)
    {
        if (payload.Length < 7)
        {
            reason = "A JBIG2 pattern-dictionary segment is truncated.";
            return false;
        }

        var patternWidth = payload[1];
        var patternHeight = payload[2];
        var grayMax = ReadUInt32BigEndian(payload[3..]);
        var patternCount = (ulong)grayMax + 1;
        if (patternWidth == 0 || patternHeight == 0 || patternCount > MaximumJbig2Symbols ||
            patternCount * patternWidth * patternHeight > MaximumPixels)
        {
            reason = $"A JBIG2 pattern dictionary exceeds the {MaximumJbig2Symbols} pattern or {MaximumPixels} pixel redaction limit.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryReadBoundedJbig2TextRegion(ReadOnlySpan<byte> payload, out string reason)
    {
        if (payload.Length < 23)
        {
            reason = "A JBIG2 text-region segment is truncated.";
            return false;
        }

        var flags = ReadUInt16BigEndian(payload[17..]);
        var countOffset = 19;
        if ((flags & 0x0001) != 0)
        {
            countOffset += 2;
        }
        if ((flags & 0x0002) != 0 && (flags & 0x8000) == 0)
        {
            countOffset += 4;
        }
        if (payload.Length - countOffset < 4)
        {
            reason = "A JBIG2 text-region segment has truncated flags or symbol-instance fields.";
            return false;
        }

        var instances = ReadUInt32BigEndian(payload[countOffset..]);
        if (instances > MaximumJbig2SymbolInstances)
        {
            reason = $"A JBIG2 text region exceeds the {MaximumJbig2SymbolInstances} symbol-instance redaction limit.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryReadBoundedJbig2HalftoneGrid(ReadOnlySpan<byte> payload, out string reason)
    {
        if (payload.Length < 26)
        {
            reason = "A JBIG2 halftone-region segment is truncated.";
            return false;
        }

        var gridWidth = ReadUInt32BigEndian(payload[18..]);
        var gridHeight = ReadUInt32BigEndian(payload[22..]);
        if (gridWidth is 0 or > int.MaxValue || gridHeight is 0 or > int.MaxValue ||
            (ulong)gridWidth * gridHeight > MaximumPixels)
        {
            reason = $"A JBIG2 halftone grid exceeds the {MaximumPixels} cell redaction limit.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryReadJbig2Segment(
        ReadOnlySpan<byte> data,
        ref int offset,
        out int type,
        out ReadOnlySpan<byte> payload,
        out string reason)
    {
        type = 0;
        payload = default;
        if (data.Length - offset < 6)
        {
            reason = "The JBIG2 segment header is truncated.";
            return false;
        }

        var segmentNumber = ReadUInt32BigEndian(data[offset..]);
        offset += 4;
        var flags = data[offset++];
        type = flags & 0x3F;
        var shortRetention = data[offset++];
        long referredCount = shortRetention >> 5;
        if (referredCount == 7)
        {
            if (data.Length - offset < 3)
            {
                reason = "The JBIG2 extended retention header is truncated.";
                return false;
            }
            referredCount = ((long)(shortRetention & 0x1F) << 24) |
                            ((long)data[offset] << 16) |
                            ((long)data[offset + 1] << 8) |
                            data[offset + 2];
            offset += 3;
            var retentionBytes = (referredCount + 1 + 7) / 8;
            if (retentionBytes > data.Length - offset)
            {
                reason = "The JBIG2 retention flags exceed the stream boundary.";
                return false;
            }
            offset += (int)retentionBytes;
        }
        else if (referredCount is 5 or 6)
        {
            reason = "The JBIG2 segment uses a reserved referred-segment count.";
            return false;
        }

        var referenceSize = segmentNumber <= 256 ? 1 : segmentNumber <= 65_536 ? 2 : 4;
        var referenceBytes = referredCount * referenceSize;
        var pageAssociationBytes = (flags & 0x40) == 0 ? 1 : 4;
        var dataLengthBytes = type == 51 ? 0 : 4;
        if (referenceBytes > data.Length - offset ||
            data.Length - offset - referenceBytes < pageAssociationBytes + dataLengthBytes)
        {
            reason = "The JBIG2 segment references exceed the stream boundary.";
            return false;
        }
        offset += (int)referenceBytes + pageAssociationBytes;

        if (type == 51)
        {
            var trailingBytes = data[offset..];
            if (trailingBytes.Length > 4 || trailingBytes.ContainsAnyExcept((byte)0))
            {
                reason = "The JBIG2 end-of-file segment is followed by unexpected data.";
                return false;
            }
            offset = data.Length;
            payload = ReadOnlySpan<byte>.Empty;
            reason = string.Empty;
            return true;
        }

        var payloadLength = ReadUInt32BigEndian(data[offset..]);
        offset += 4;
        if (payloadLength == uint.MaxValue || payloadLength > data.Length - offset)
        {
            reason = "The JBIG2 segment has an unknown or out-of-bounds payload length.";
            return false;
        }
        payload = data.Slice(offset, (int)payloadLength);
        offset += (int)payloadLength;
        reason = string.Empty;
        return true;
    }

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> value) =>
        ((uint)value[0] << 24) | ((uint)value[1] << 16) | ((uint)value[2] << 8) | value[3];

    private static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> value) =>
        (ushort)((value[0] << 8) | value[1]);

    private static ulong ReadUInt64BigEndian(ReadOnlySpan<byte> value) =>
        ((ulong)ReadUInt32BigEndian(value) << 32) | ReadUInt32BigEndian(value[4..]);

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
