using PXA.Importer.Objects;

namespace PXA.Importer.Color;

public enum PdfColorSpaceFamily
{
    DeviceGray,
    DeviceRgb,
    DeviceCmyk,
    CalGray,
    CalRgb,
    Lab,
    IccBased,
    Indexed,
    Separation,
    DeviceN,
    Pattern,
    Unsupported
}

/// <summary>A resolved colour space and everything needed to map its components to sRGB.</summary>
public sealed record PdfResolvedColorSpace(
    PdfColorSpaceFamily Family,
    int ComponentCount,
    PdfResolvedColorSpace? Alternate = null,
    PdfFunction? TintTransform = null,
    byte[]? IndexedLookup = null,
    int IndexedHighValue = 0,
    IReadOnlyList<double>? LabRange = null,
    IReadOnlyList<double>? LabWhitePoint = null)
{
    /// <summary>True when every colour in this space can be mapped to sRGB deterministically.</summary>
    public bool IsConvertible => Family switch
    {
        PdfColorSpaceFamily.Pattern or PdfColorSpaceFamily.Unsupported or
            PdfColorSpaceFamily.CalGray or PdfColorSpaceFamily.CalRgb or PdfColorSpaceFamily.Lab => false,
        PdfColorSpaceFamily.Separation or PdfColorSpaceFamily.DeviceN =>
            TintTransform is not null && Alternate is { IsConvertible: true },
        PdfColorSpaceFamily.Indexed => Alternate is { IsConvertible: true } && IndexedLookup is not null,
        PdfColorSpaceFamily.IccBased => Alternate is { IsConvertible: true },
        _ => true
    };
}

/// <summary>
/// Converts PDF colour values to sRGB.
///
/// The conversions are deterministic and fully specified here rather than delegated to a
/// colour management system, so the same input always produces the same output. DeviceCMYK
/// uses the conversion given in ISO 32000-1, 8.6.4.4, which is an approximation of a real
/// ICC transform: it is reproducible, not colorimetrically exact.
/// </summary>
public static class PdfSrgbColorConverter
{
    private const int MaximumDepth = 8;

    public static PdfResolvedColorSpace Resolve(
        PdfObject? value,
        Func<PdfObject?, PdfObject?> resolve,
        int depth = 0)
    {
        if (depth > MaximumDepth)
        {
            return new PdfResolvedColorSpace(PdfColorSpaceFamily.Unsupported, 0);
        }

        var resolved = resolve(value);
        if (resolved is PdfName name)
        {
            return FromName(name.Value);
        }

        if (resolved is not PdfArray array || array.Items.Count == 0)
        {
            return new PdfResolvedColorSpace(PdfColorSpaceFamily.Unsupported, 0);
        }

        var family = (resolve(array.Items[0]) as PdfName)?.Value ?? string.Empty;
        switch (family)
        {
            case "DeviceGray" or "G":
                return new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceGray, 1);
            case "DeviceRGB" or "RGB":
                return new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceRgb, 3);
            case "DeviceCMYK" or "CMYK":
                return new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceCmyk, 4);
            case "CalGray":
                return new PdfResolvedColorSpace(PdfColorSpaceFamily.CalGray, 1);
            case "CalRGB":
                return new PdfResolvedColorSpace(PdfColorSpaceFamily.CalRgb, 3);
            case "Lab":
            {
                var parameters = resolve(array.Items.ElementAtOrDefault(1)) as PdfDictionary;
                return new PdfResolvedColorSpace(
                    PdfColorSpaceFamily.Lab,
                    3,
                    LabRange: PdfFunction.ReadNumbers(parameters?["Range"], resolve),
                    LabWhitePoint: PdfFunction.ReadNumbers(parameters?["WhitePoint"], resolve));
            }

            case "ICCBased":
            {
                var stream = resolve(array.Items.ElementAtOrDefault(1)) as PdfStreamObject;
                var components = stream is null
                    ? 3
                    : (int)PdfFunction.ReadNumber(stream.Dictionary["N"], resolve, 3);
                var alternate = stream?.Dictionary["Alternate"] is { } alternateValue
                    ? Resolve(alternateValue, resolve, depth + 1)
                    : null;
                return new PdfResolvedColorSpace(
                    PdfColorSpaceFamily.IccBased,
                    components,
                    alternate);
            }

            case "Indexed" or "I":
            {
                var baseSpace = Resolve(array.Items.ElementAtOrDefault(1), resolve, depth + 1);
                var high = (int)PdfFunction.ReadNumber(array.Items.ElementAtOrDefault(2), resolve, 0);
                var lookup = resolve(array.Items.ElementAtOrDefault(3)) switch
                {
                    PdfString text => text.GetDecodedBytes().ToArray(),
                    PdfStreamObject stream => TryDecode(stream),
                    _ => null
                };
                return new PdfResolvedColorSpace(
                    PdfColorSpaceFamily.Indexed, 1, baseSpace, IndexedLookup: lookup, IndexedHighValue: high);
            }

            case "Separation":
            {
                var alternate = Resolve(array.Items.ElementAtOrDefault(2), resolve, depth + 1);
                var tint = PdfFunction.TryCreate(array.Items.ElementAtOrDefault(3), resolve);
                return new PdfResolvedColorSpace(PdfColorSpaceFamily.Separation, 1, alternate, tint);
            }

            case "DeviceN":
            {
                var names = resolve(array.Items.ElementAtOrDefault(1)) as PdfArray;
                var alternate = Resolve(array.Items.ElementAtOrDefault(2), resolve, depth + 1);
                var tint = PdfFunction.TryCreate(array.Items.ElementAtOrDefault(3), resolve);
                return new PdfResolvedColorSpace(
                    PdfColorSpaceFamily.DeviceN, names?.Items.Count ?? 1, alternate, tint);
            }

            case "Pattern":
                return new PdfResolvedColorSpace(PdfColorSpaceFamily.Pattern, 1);
            default:
                return new PdfResolvedColorSpace(PdfColorSpaceFamily.Unsupported, 0);
        }
    }

    /// <summary>
    /// Maps component values in <paramref name="space"/> to sRGB in the range 0..1. Returns
    /// null when the space cannot be converted deterministically.
    /// </summary>
    public static double[]? ToSrgb(PdfResolvedColorSpace space, IReadOnlyList<double> components, int depth = 0)
    {
        if (depth > MaximumDepth)
        {
            return null;
        }

        switch (space.Family)
        {
            case PdfColorSpaceFamily.DeviceGray or PdfColorSpaceFamily.CalGray:
            {
                var gray = Clamp(Component(components, 0));
                return [gray, gray, gray];
            }

            case PdfColorSpaceFamily.DeviceRgb or PdfColorSpaceFamily.CalRgb:
                return [Clamp(Component(components, 0)), Clamp(Component(components, 1)), Clamp(Component(components, 2))];

            case PdfColorSpaceFamily.DeviceCmyk:
            {
                // ISO 32000-1, 8.6.4.4.
                var c = Clamp(Component(components, 0));
                var m = Clamp(Component(components, 1));
                var y = Clamp(Component(components, 2));
                var k = Clamp(Component(components, 3));
                return [1d - Math.Min(1d, c + k), 1d - Math.Min(1d, m + k), 1d - Math.Min(1d, y + k)];
            }

            case PdfColorSpaceFamily.Lab:
                return LabToSrgb(space, components);

            case PdfColorSpaceFamily.IccBased:
                return space.Alternate is null
                    ? null
                    : ToSrgb(space.Alternate, components, depth + 1);

            case PdfColorSpaceFamily.Indexed:
            {
                if (space.Alternate is null || space.IndexedLookup is null)
                {
                    return null;
                }

                var index = (int)Math.Round(Math.Clamp(Component(components, 0), 0, space.IndexedHighValue));
                var baseCount = space.Alternate.ComponentCount;
                var offset = index * baseCount;
                if (offset < 0 || offset + baseCount > space.IndexedLookup.Length)
                {
                    return null;
                }

                var baseComponents = new double[baseCount];
                for (var component = 0; component < baseCount; component++)
                {
                    baseComponents[component] = space.IndexedLookup[offset + component] / 255d;
                }

                return ToSrgb(space.Alternate, baseComponents, depth + 1);
            }

            case PdfColorSpaceFamily.Separation or PdfColorSpaceFamily.DeviceN:
            {
                if (space.TintTransform is null || space.Alternate is null)
                {
                    return null;
                }

                var tint = space.TintTransform.Evaluate(components.Select(Clamp).ToArray());
                return ToSrgb(space.Alternate, tint, depth + 1);
            }

            default:
                return null;
        }
    }

    private static double[]? LabToSrgb(PdfResolvedColorSpace space, IReadOnlyList<double> components)
    {
        var white = space.LabWhitePoint is { Count: >= 3 }
            ? space.LabWhitePoint
            : new[] { 0.9505d, 1.0d, 1.089d };
        var lightness = Math.Clamp(Component(components, 0), 0, 100);
        var a = Component(components, 1);
        var b = Component(components, 2);

        var m = (lightness + 16d) / 116d;
        var l = m + a / 500d;
        var n = m - b / 200d;

        static double Inverse(double value) => value >= 6d / 29d
            ? value * value * value
            : 108d / 841d * (value - 4d / 29d);

        var x = white[0] * Inverse(l);
        var y = white[1] * Inverse(m);
        var z = white[2] * Inverse(n);

        // XYZ (D50) to linear sRGB, then the sRGB transfer function.
        var red = 3.1339d * x - 1.6169d * y - 0.4906d * z;
        var green = -0.9785d * x + 1.9160d * y + 0.0333d * z;
        var blue = 0.0720d * x - 0.2290d * y + 1.4057d * z;
        return [Gamma(red), Gamma(green), Gamma(blue)];

        static double Gamma(double value)
        {
            var linear = Math.Clamp(value, 0d, 1d);
            return linear <= 0.0031308d
                ? 12.92d * linear
                : 1.055d * Math.Pow(linear, 1d / 2.4d) - 0.055d;
        }
    }

    private static PdfResolvedColorSpace FromName(string name) => name switch
    {
        "DeviceGray" or "G" or "CalGray" => new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceGray, 1),
        "DeviceRGB" or "RGB" or "CalRGB" => new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceRgb, 3),
        "DeviceCMYK" or "CMYK" => new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceCmyk, 4),
        "Pattern" => new PdfResolvedColorSpace(PdfColorSpaceFamily.Pattern, 1),
        _ => new PdfResolvedColorSpace(PdfColorSpaceFamily.Unsupported, 0)
    };

    private static PdfResolvedColorSpace FromComponentCount(int components) => components switch
    {
        1 => new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceGray, 1),
        4 => new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceCmyk, 4),
        _ => new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceRgb, 3)
    };

    private static byte[]? TryDecode(PdfStreamObject stream)
    {
        try
        {
            return new Streams.PdfStreamDecoderRegistry().Decode(stream).ToArray();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static double Component(IReadOnlyList<double> components, int index) =>
        index < components.Count ? components[index] : 0d;

    private static double Clamp(double value) => Math.Clamp(value, 0d, 1d);
}
