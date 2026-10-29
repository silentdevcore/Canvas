using PVAD.Importer.Objects;

namespace PVAD.Importer.Color;

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
    IReadOnlyList<double>? WhitePoint = null,
    IReadOnlyList<double>? BlackPoint = null,
    IReadOnlyList<double>? Gamma = null,
    IReadOnlyList<double>? Matrix = null,
    byte[]? IccProfile = null,
    bool ParametersValid = true)
{
    /// <summary>True when every colour in this space can be mapped to sRGB deterministically.</summary>
    public bool IsConvertible => Family switch
    {
        PdfColorSpaceFamily.Pattern or PdfColorSpaceFamily.Unsupported => false,
        PdfColorSpaceFamily.CalGray or PdfColorSpaceFamily.CalRgb or PdfColorSpaceFamily.Lab => ParametersValid,
        PdfColorSpaceFamily.Separation or PdfColorSpaceFamily.DeviceN =>
            TintTransform is not null && Alternate is { IsConvertible: true },
        PdfColorSpaceFamily.Indexed => Alternate is { IsConvertible: true } && IndexedLookup is not null,
        PdfColorSpaceFamily.IccBased => IccProfile is { Length: >= 128 } profile &&
            ComponentCount is 1 or 3 or 4 && PdfIccColorTransform.IsValidProfile(profile),
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
            {
                var parameters = resolve(array.Items.ElementAtOrDefault(1)) as PdfDictionary;
                var whitePoint = ReadRequiredWhitePoint(parameters, resolve);
                var blackPoint = ReadBlackPoint(parameters, resolve);
                var gamma = PdfFunction.ReadNumber(parameters?["Gamma"], resolve, 1d);
                return new PdfResolvedColorSpace(
                    PdfColorSpaceFamily.CalGray,
                    1,
                    WhitePoint: whitePoint,
                    BlackPoint: blackPoint,
                    Gamma: [gamma],
                    ParametersValid: IsValidWhitePoint(whitePoint) &&
                        IsValidBlackPoint(blackPoint) && gamma > 0d && double.IsFinite(gamma));
            }
            case "CalRGB":
            {
                var parameters = resolve(array.Items.ElementAtOrDefault(1)) as PdfDictionary;
                var whitePoint = ReadRequiredWhitePoint(parameters, resolve);
                var blackPoint = ReadBlackPoint(parameters, resolve);
                var gamma = ReadWithDefault(parameters?["Gamma"], resolve, [1d, 1d, 1d]);
                var matrix = ReadWithDefault(
                    parameters?["Matrix"],
                    resolve,
                    [1d, 0d, 0d, 0d, 1d, 0d, 0d, 0d, 1d]);
                return new PdfResolvedColorSpace(
                    PdfColorSpaceFamily.CalRgb,
                    3,
                    WhitePoint: whitePoint,
                    BlackPoint: blackPoint,
                    Gamma: gamma,
                    Matrix: matrix,
                    ParametersValid: IsValidWhitePoint(whitePoint) &&
                        IsValidBlackPoint(blackPoint) && gamma.Count == 3 &&
                        gamma.All(static value => value > 0d && double.IsFinite(value)) &&
                        matrix.Count == 9 && matrix.All(double.IsFinite));
            }
            case "Lab":
            {
                var parameters = resolve(array.Items.ElementAtOrDefault(1)) as PdfDictionary;
                return new PdfResolvedColorSpace(
                    PdfColorSpaceFamily.Lab,
                    3,
                    LabRange: PdfFunction.ReadNumbers(parameters?["Range"], resolve),
                    WhitePoint: ReadRequiredWhitePoint(parameters, resolve),
                    BlackPoint: ReadBlackPoint(parameters, resolve),
                    ParametersValid: IsValidLab(parameters, resolve));
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
                    alternate,
                    IccProfile: stream is null ? null : TryDecode(stream));
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
            {
                var alternate = array.Items.Count > 1
                    ? Resolve(array.Items[1], resolve, depth + 1)
                    : null;
                return new PdfResolvedColorSpace(
                    PdfColorSpaceFamily.Pattern,
                    (alternate?.ComponentCount ?? 0) + 1,
                    alternate);
            }
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
            case PdfColorSpaceFamily.DeviceGray:
            {
                var gray = Clamp(Component(components, 0));
                return [gray, gray, gray];
            }

            case PdfColorSpaceFamily.DeviceRgb:
                return [Clamp(Component(components, 0)), Clamp(Component(components, 1)), Clamp(Component(components, 2))];

            case PdfColorSpaceFamily.CalGray:
                return CalGrayToSrgb(space, components);

            case PdfColorSpaceFamily.CalRgb:
                return CalRgbToSrgb(space, components);

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
                return space.IccProfile is null
                    ? null
                    : PdfIccColorTransform.ToSrgb(space.IccProfile, space.ComponentCount, components);

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
        if (!space.ParametersValid || space.WhitePoint is not { Count: 3 } white)
        {
            return null;
        }

        var lightness = Math.Clamp(Component(components, 0), 0, 100);
        var range = space.LabRange is { Count: 4 } configuredRange
            ? configuredRange
            : new[] { -100d, 100d, -100d, 100d };
        var a = Math.Clamp(Component(components, 1), range[0], range[1]);
        var b = Math.Clamp(Component(components, 2), range[2], range[3]);

        var m = (lightness + 16d) / 116d;
        var l = m + a / 500d;
        var n = m - b / 200d;

        static double Inverse(double value) => value >= 6d / 29d
            ? value * value * value
            : 108d / 841d * (value - 4d / 29d);

        var x = white[0] * Inverse(l);
        var y = white[1] * Inverse(m);
        var z = white[2] * Inverse(n);

        return XyzToSrgb(x, y, z, white);
    }

    private static double[]? CalGrayToSrgb(PdfResolvedColorSpace space, IReadOnlyList<double> components)
    {
        if (!space.ParametersValid || space.WhitePoint is not { Count: 3 } white ||
            space.Gamma is not { Count: 1 } gamma)
        {
            return null;
        }

        var value = Math.Pow(Clamp(Component(components, 0)), gamma[0]);
        return XyzToSrgb(white[0] * value, white[1] * value, white[2] * value, white);
    }

    private static double[]? CalRgbToSrgb(PdfResolvedColorSpace space, IReadOnlyList<double> components)
    {
        if (!space.ParametersValid || space.WhitePoint is not { Count: 3 } white ||
            space.Gamma is not { Count: 3 } gamma || space.Matrix is not { Count: 9 } matrix)
        {
            return null;
        }

        var a = Math.Pow(Clamp(Component(components, 0)), gamma[0]);
        var b = Math.Pow(Clamp(Component(components, 1)), gamma[1]);
        var c = Math.Pow(Clamp(Component(components, 2)), gamma[2]);
        var x = matrix[0] * a + matrix[3] * b + matrix[6] * c;
        var y = matrix[1] * a + matrix[4] * b + matrix[7] * c;
        var z = matrix[2] * a + matrix[5] * b + matrix[8] * c;
        return XyzToSrgb(x, y, z, white);
    }

    private static double[] XyzToSrgb(double x, double y, double z, IReadOnlyList<double> sourceWhite)
    {
        // Bradford chromatic adaptation from the calibrated source white to sRGB D65.
        var sourceCone = MultiplyBradford(sourceWhite[0], sourceWhite[1], sourceWhite[2]);
        var targetCone = MultiplyBradford(0.95047d, 1d, 1.08883d);
        var cone = MultiplyBradford(x, y, z);
        var adapted = MultiplyBradfordInverse(
            cone[0] * targetCone[0] / sourceCone[0],
            cone[1] * targetCone[1] / sourceCone[1],
            cone[2] * targetCone[2] / sourceCone[2]);

        var red = 3.2406d * adapted[0] - 1.5372d * adapted[1] - 0.4986d * adapted[2];
        var green = -0.9689d * adapted[0] + 1.8758d * adapted[1] + 0.0415d * adapted[2];
        var blue = 0.0557d * adapted[0] - 0.2040d * adapted[1] + 1.0570d * adapted[2];
        return [EncodeSrgb(red), EncodeSrgb(green), EncodeSrgb(blue)];

        static double[] MultiplyBradford(double x, double y, double z) =>
        [
            0.8951d * x + 0.2664d * y - 0.1614d * z,
            -0.7502d * x + 1.7135d * y + 0.0367d * z,
            0.0389d * x - 0.0685d * y + 1.0296d * z
        ];

        static double[] MultiplyBradfordInverse(double l, double m, double s) =>
        [
            0.9869929d * l - 0.1470543d * m + 0.1599627d * s,
            0.4323053d * l + 0.5183603d * m + 0.0492912d * s,
            -0.0085287d * l + 0.0400428d * m + 0.9684867d * s
        ];

        static double EncodeSrgb(double value)
        {
            var linear = Math.Clamp(value, 0d, 1d);
            return linear <= 0.0031308d
                ? 12.92d * linear
                : 1.055d * Math.Pow(linear, 1d / 2.4d) - 0.055d;
        }
    }

    private static IReadOnlyList<double>? ReadRequiredWhitePoint(
        PdfDictionary? parameters,
        Func<PdfObject?, PdfObject?> resolve) =>
        parameters?["WhitePoint"] is null
            ? null
            : PdfFunction.ReadNumbers(parameters["WhitePoint"], resolve);

    private static IReadOnlyList<double> ReadBlackPoint(
        PdfDictionary? parameters,
        Func<PdfObject?, PdfObject?> resolve) =>
        parameters?["BlackPoint"] is null
            ? [0d, 0d, 0d]
            : PdfFunction.ReadNumbers(parameters["BlackPoint"], resolve);

    private static IReadOnlyList<double> ReadWithDefault(
        PdfObject? value,
        Func<PdfObject?, PdfObject?> resolve,
        IReadOnlyList<double> fallback) =>
        value is null ? fallback : PdfFunction.ReadNumbers(value, resolve);

    private static bool IsValidWhitePoint(IReadOnlyList<double>? value) =>
        value is { Count: 3 } && value.All(double.IsFinite) &&
        value[0] > 0d && Math.Abs(value[1] - 1d) <= 0.000001d && value[2] > 0d;

    private static bool IsValidBlackPoint(IReadOnlyList<double>? value) =>
        value is { Count: 3 } && value.All(static component => component >= 0d && double.IsFinite(component));

    private static bool IsValidLab(PdfDictionary? parameters, Func<PdfObject?, PdfObject?> resolve)
    {
        var whitePoint = ReadRequiredWhitePoint(parameters, resolve);
        var blackPoint = ReadBlackPoint(parameters, resolve);
        var range = parameters?["Range"] is null
            ? new[] { -100d, 100d, -100d, 100d }
            : PdfFunction.ReadNumbers(parameters["Range"], resolve);
        return IsValidWhitePoint(whitePoint) && IsValidBlackPoint(blackPoint) &&
            range.Count == 4 && range.All(double.IsFinite) &&
            range[0] < range[1] && range[2] < range[3];
    }

    private static PdfResolvedColorSpace FromName(string name) => name switch
    {
        "DeviceGray" or "G" => new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceGray, 1),
        "DeviceRGB" or "RGB" => new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceRgb, 3),
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
