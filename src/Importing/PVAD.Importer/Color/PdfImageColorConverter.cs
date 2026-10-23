using PVAD.Importer.Objects;
using PVAD.Importer.Streams;

namespace PVAD.Importer.Color;

internal sealed record PdfImageColorConversionResult(
    bool Converted,
    PdfStreamObject? Stream = null,
    string? BlockingReason = null);

/// <summary>
/// Converts bounded, losslessly decodable 8-bit image samples to DeviceRGB. Encoded image
/// formats and colour-key masks stay fail-closed because rewriting them requires format-aware
/// pixel and mask handling rather than treating compressed bytes as samples.
/// </summary>
internal static class PdfImageColorConverter
{
    private const long MaximumPixels = 25_000_000;
    private static readonly HashSet<string> LosslessFilters = new(StringComparer.Ordinal)
    {
        "FlateDecode", "Fl", "LZWDecode", "LZW", "ASCIIHexDecode", "AHx", "ASCII85Decode", "A85"
    };

    public static PdfImageColorConversionResult Convert(
        PdfStreamObject image,
        Func<PdfObject?, PdfObject?> resolve)
    {
        // Stencil masks paint with the current graphics-state colour and intentionally have
        // no /ColorSpace. Their one-bit samples must not be treated as coloured image data.
        if (resolve(image.Dictionary["ImageMask"]) is PdfBoolean { Value: true })
        {
            return new PdfImageColorConversionResult(false);
        }

        var space = PdfSrgbColorConverter.Resolve(image.Dictionary["ColorSpace"], resolve);
        if (space.Family is PdfColorSpaceFamily.DeviceGray or PdfColorSpaceFamily.DeviceRgb)
        {
            return new PdfImageColorConversionResult(false);
        }

        if (space.Family is not (PdfColorSpaceFamily.DeviceCmyk or PdfColorSpaceFamily.Indexed or
            PdfColorSpaceFamily.Separation or PdfColorSpaceFamily.DeviceN) || !space.IsConvertible)
        {
            return Block($"The {Describe(space)} image colour space does not have a complete deterministic conversion path.");
        }

        if (ReadInteger(resolve(image.Dictionary["BitsPerComponent"])) != 8)
        {
            return Block("Only 8-bit image samples can currently be converted to sRGB.");
        }

        var width = ReadInteger(resolve(image.Dictionary["Width"]));
        var height = ReadInteger(resolve(image.Dictionary["Height"]));
        if (width is null or <= 0 || height is null or <= 0 || (long)width * height > MaximumPixels)
        {
            return Block($"Image dimensions are invalid or exceed the {MaximumPixels:N0}-pixel conversion limit.");
        }

        if (resolve(image.Dictionary["Mask"]) is PdfArray)
        {
            return Block("Colour-key image masks cannot yet be transformed with converted samples.");
        }

        if (!image.IsDecoded && !HasOnlyLosslessFilters(image.Dictionary["Filter"], resolve))
        {
            return Block("The image uses an encoded or unknown filter that cannot be rewritten as raw colour samples safely.");
        }

        byte[] samples;
        try
        {
            samples = image.IsDecoded
                ? image.DecodedBytes.ToArray()
                : new PdfStreamDecoderRegistry().Decode(image).ToArray();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Block("The image samples could not be decoded within the configured safety limits.");
        }

        var components = space.ComponentCount;
        var expectedLength = checked((long)width.Value * height.Value * components);
        if (expectedLength != samples.LongLength)
        {
            return Block($"Decoded image data has {samples.LongLength} bytes; {expectedLength} bytes are required by its dimensions and colour space.");
        }

        var decode = ReadNumbers(resolve(image.Dictionary["Decode"]));
        if (decode is not null && decode.Count != components * 2)
        {
            return Block("The image /Decode array does not match its colour component count.");
        }

        var output = new byte[checked(width.Value * height.Value * 3)];
        var inputComponents = new double[components];
        try
        {
            for (var pixel = 0; pixel < width.Value * height.Value; pixel++)
            {
                for (var component = 0; component < components; component++)
                {
                    var sample = samples[pixel * components + component] / 255d;
                    inputComponents[component] = decode is not null
                        ? decode[component * 2] + sample * (decode[component * 2 + 1] - decode[component * 2])
                        : space.Family == PdfColorSpaceFamily.Indexed
                            ? samples[pixel * components + component]
                            : sample;
                }

                var rgb = PdfSrgbColorConverter.ToSrgb(space, inputComponents);
                if (rgb is not { Length: >= 3 })
                {
                    return Block("An image sample could not be mapped through its colour space to sRGB.");
                }

                for (var component = 0; component < 3; component++)
                {
                    output[pixel * 3 + component] = (byte)Math.Round(
                        Math.Clamp(rgb[component], 0d, 1d) * 255d,
                        MidpointRounding.AwayFromZero);
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or OverflowException)
        {
            return Block("The image colour function could not be evaluated safely for every sample.");
        }

        var dictionary = new PdfDictionary(new Dictionary<string, PdfObject>(image.Dictionary.Values, StringComparer.Ordinal));
        dictionary["ColorSpace"] = new PdfName("DeviceRGB");
        dictionary["BitsPerComponent"] = new PdfInteger(8);
        dictionary["Decode"] = null;
        dictionary["Filter"] = null;
        dictionary["DecodeParms"] = null;
        dictionary["Length"] = new PdfInteger(output.Length);
        return new PdfImageColorConversionResult(true, new PdfStreamObject(dictionary, output));
    }

    private static bool HasOnlyLosslessFilters(PdfObject? value, Func<PdfObject?, PdfObject?> resolve)
    {
        var resolved = resolve(value);
        return resolved switch
        {
            null => true,
            PdfName name => LosslessFilters.Contains(name.Value),
            PdfArray array => array.Items.Count > 0 && array.Items.All(item =>
                resolve(item) is PdfName filter && LosslessFilters.Contains(filter.Value)),
            _ => false
        };
    }

    private static int? ReadInteger(PdfObject? value) => value switch
    {
        PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
        PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
        _ => null
    };

    private static IReadOnlyList<double>? ReadNumbers(PdfObject? value)
    {
        if (value is not PdfArray array)
        {
            return null;
        }

        var numbers = new List<double>(array.Items.Count);
        foreach (var item in array.Items)
        {
            switch (item)
            {
                case PdfInteger integer:
                    numbers.Add(integer.Value);
                    break;
                case PdfNumber number:
                    numbers.Add(number.Value);
                    break;
                default:
                    return [];
            }
        }

        return numbers;
    }

    private static string Describe(PdfResolvedColorSpace space) => space.Family switch
    {
        PdfColorSpaceFamily.CalGray => "CalGray",
        PdfColorSpaceFamily.CalRgb => "CalRGB",
        PdfColorSpaceFamily.Lab => "Lab",
        PdfColorSpaceFamily.IccBased => "ICCBased",
        PdfColorSpaceFamily.Pattern => "Pattern",
        PdfColorSpaceFamily.Unsupported => "unknown",
        _ => space.Family.ToString()
    };

    private static PdfImageColorConversionResult Block(string reason) =>
        new(false, BlockingReason: reason);
}
