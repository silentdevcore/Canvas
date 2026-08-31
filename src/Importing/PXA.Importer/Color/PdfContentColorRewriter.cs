using System.Globalization;
using System.Text;
using PXA.Importer.Content;
using PXA.Importer.Objects;

namespace PXA.Importer.Color;

public sealed record PdfColorRewriteResult(
    byte[] Content,
    int ConvertedOperatorCount,
    IReadOnlyList<string> UnconvertibleColorSpaceNames);

/// <summary>
/// Rewrites colour operators in a content stream to DeviceRGB.
///
/// The rewrite is a splice: only the byte ranges of the colour commands themselves are
/// replaced, and every other byte — including inline images and text — is copied through
/// unchanged. Re-serializing the whole stream from an interpreted model would risk changing
/// content the conversion is not supposed to touch.
/// </summary>
public sealed class PdfContentColorRewriter
{
    private readonly Func<PdfObject?, PdfObject?> _resolve;
    private readonly IReadOnlyDictionary<string, PdfObject> _colorSpaceResources;
    private readonly IReadOnlyDictionary<string, PdfObject> _patternResources;

    public PdfContentColorRewriter(
        Func<PdfObject?, PdfObject?> resolve,
        IReadOnlyDictionary<string, PdfObject> colorSpaceResources,
        IReadOnlyDictionary<string, PdfObject>? patternResources = null)
    {
        _resolve = resolve;
        _colorSpaceResources = colorSpaceResources;
        _patternResources = patternResources ?? new Dictionary<string, PdfObject>(StringComparer.Ordinal);
    }

    public PdfColorRewriteResult Rewrite(ReadOnlyMemory<byte> content)
    {
        var commands = new PdfContentStreamParser().Parse(content);
        var edits = new List<(int Start, int End, string Replacement)>();
        var unconvertible = new SortedSet<string>(StringComparer.Ordinal);
        var converted = 0;

        PdfResolvedColorSpace? fillSpace = new(PdfColorSpaceFamily.DeviceGray, 1);
        PdfResolvedColorSpace? strokeSpace = new(PdfColorSpaceFamily.DeviceGray, 1);
        string? fillSpaceName = null;
        string? strokeSpaceName = null;
        var graphicsStates = new Stack<ColorGraphicsState>();

        foreach (var command in commands)
        {
            switch (command.Operator.Name)
            {
                case "q":
                    graphicsStates.Push(new ColorGraphicsState(fillSpace, strokeSpace, fillSpaceName, strokeSpaceName));
                    break;
                case "Q":
                    if (graphicsStates.TryPop(out var restored))
                    {
                        fillSpace = restored.FillSpace;
                        strokeSpace = restored.StrokeSpace;
                        fillSpaceName = restored.FillSpaceName;
                        strokeSpaceName = restored.StrokeSpaceName;
                    }
                    break;

                case "cs" or "CS":
                {
                    var stroke = command.Operator.Name == "CS";
                    var name = (command.Operands.FirstOrDefault() as PdfName)?.Value;
                    var space = ResolveNamed(name);
                    if (stroke)
                    {
                        strokeSpace = space;
                        strokeSpaceName = name;
                    }
                    else
                    {
                        fillSpace = space;
                        fillSpaceName = name;
                    }

                    // A selection of a convertible space is replaced by /DeviceRGB; the
                    // following sc/scn supplies the converted components.
                    if (space is { Family: PdfColorSpaceFamily.Pattern })
                    {
                        // Pattern selection remains a Pattern colour space. The selected
                        // coloured tiling pattern is validated below and its own content
                        // stream is converted independently.
                    }
                    else if (space is not null && space.IsConvertible && space.Family != PdfColorSpaceFamily.DeviceRgb)
                    {
                        edits.Add((SpanStart(command), SpanEnd(command), stroke ? "/DeviceRGB CS" : "/DeviceRGB cs"));
                        converted++;
                    }
                    else if ((space is null || !space.IsConvertible) && name is not null)
                    {
                        unconvertible.Add(name);
                    }

                    break;
                }

                case "BI":
                {
                    var inlineImage = command.Operands.OfType<PdfStreamObject>().FirstOrDefault();
                    var imageMask = inlineImage?.Dictionary["ImageMask"] ?? inlineImage?.Dictionary["IM"];
                    if (imageMask is PdfBoolean { Value: true })
                    {
                        break;
                    }

                    var colorSpaceObject = inlineImage?.Dictionary["ColorSpace"] ?? inlineImage?.Dictionary["CS"];
                    var inlineSpace = colorSpaceObject is null
                        ? new PdfResolvedColorSpace(PdfColorSpaceFamily.Unsupported, 0)
                        : PdfSrgbColorConverter.Resolve(colorSpaceObject, _resolve);
                    if (inlineSpace.Family is not (PdfColorSpaceFamily.DeviceGray or PdfColorSpaceFamily.DeviceRgb))
                    {
                        unconvertible.Add("InlineImage");
                    }
                    break;
                }

                case "sh":
                {
                    var name = (command.Operands.FirstOrDefault() as PdfName)?.Value;
                    unconvertible.Add(name is null ? "Shading" : $"Shading:{name}");
                    break;
                }

                case "sc" or "scn" or "SC" or "SCN":
                {
                    var stroke = command.Operator.Name is "SC" or "SCN";
                    var space = stroke ? strokeSpace : fillSpace;
                    var spaceName = stroke ? strokeSpaceName : fillSpaceName;
                    if (space is null)
                    {
                        break;
                    }

                    if (space.Family == PdfColorSpaceFamily.Pattern)
                    {
                        var patternName = command.Operands.OfType<PdfName>().LastOrDefault()?.Value;
                        if (!IsSupportedColoredTilingPattern(patternName) && spaceName is not null)
                        {
                            unconvertible.Add(patternName is null
                                ? spaceName
                                : $"Pattern:{patternName}");
                        }

                        break;
                    }

                    if (!space.IsConvertible)
                    {
                        if (spaceName is not null)
                        {
                            unconvertible.Add(spaceName);
                        }

                        break;
                    }

                    if (space.Family == PdfColorSpaceFamily.DeviceRgb)
                    {
                        break;
                    }

                    var rgb = PdfSrgbColorConverter.ToSrgb(space, ReadNumbers(command.Operands));
                    if (rgb is null)
                    {
                        if (spaceName is not null)
                        {
                            unconvertible.Add(spaceName);
                        }

                        break;
                    }

                    edits.Add((SpanStart(command), SpanEnd(command), Format(rgb, stroke ? "RG" : "rg")));
                    converted++;
                    break;
                }

                case "k" or "K":
                {
                    var stroke = command.Operator.Name == "K";
                    var rgb = PdfSrgbColorConverter.ToSrgb(
                        new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceCmyk, 4),
                        ReadNumbers(command.Operands));
                    if (rgb is null)
                    {
                        break;
                    }

                    edits.Add((SpanStart(command), SpanEnd(command), Format(rgb, stroke ? "RG" : "rg")));
                    converted++;
                    if (stroke)
                    {
                        strokeSpace = new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceRgb, 3);
                        strokeSpaceName = null;
                    }
                    else
                    {
                        fillSpace = new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceRgb, 3);
                        fillSpaceName = null;
                    }

                    break;
                }

                case "rg":
                    fillSpace = new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceRgb, 3);
                    fillSpaceName = null;
                    break;
                case "RG":
                    strokeSpace = new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceRgb, 3);
                    strokeSpaceName = null;
                    break;
                case "g":
                    fillSpace = new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceGray, 1);
                    fillSpaceName = null;
                    break;
                case "G":
                    strokeSpace = new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceGray, 1);
                    strokeSpaceName = null;
                    break;
            }
        }

        return new PdfColorRewriteResult(Splice(content.Span, edits), converted, unconvertible.ToArray());
    }

    private PdfResolvedColorSpace? ResolveNamed(string? name)
    {
        if (name is null)
        {
            return null;
        }

        if (name is "DeviceGray" or "DeviceRGB" or "DeviceCMYK" or "Pattern" or "G" or "RGB" or "CMYK")
        {
            return PdfSrgbColorConverter.Resolve(new PdfName(name), _resolve);
        }

        return _colorSpaceResources.TryGetValue(name, out var resource)
            ? PdfSrgbColorConverter.Resolve(resource, _resolve)
            : null;
    }

    private bool IsSupportedColoredTilingPattern(string? name)
    {
        if (name is null || !_patternResources.TryGetValue(name, out var resource) ||
            _resolve(resource) is not PdfStreamObject pattern)
        {
            return false;
        }

        return ReadInteger(_resolve(pattern.Dictionary["PatternType"])) == 1 &&
            ReadInteger(_resolve(pattern.Dictionary["PaintType"])) == 1;
    }

    private static int? ReadInteger(PdfObject? value) => value switch
    {
        PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
        PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
        _ => null
    };

    private static byte[] Splice(ReadOnlySpan<byte> content, List<(int Start, int End, string Replacement)> edits)
    {
        if (edits.Count == 0)
        {
            return content.ToArray();
        }

        edits.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        using var output = new MemoryStream(content.Length + 64);
        var position = 0;
        foreach (var (start, end, replacement) in edits)
        {
            if (start < position || end > content.Length || end < start)
            {
                continue;
            }

            output.Write(content[position..start]);
            output.Write(Encoding.ASCII.GetBytes(replacement));
            position = end;
        }

        output.Write(content[position..]);
        return output.ToArray();
    }

    private static int SpanStart(PdfContentCommand command)
    {
        var start = (int)command.SourceSpan.Offset;
        foreach (var operand in command.Operands)
        {
            if (operand.SourceSpan.Length > 0)
            {
                start = Math.Min(start, (int)operand.SourceSpan.Offset);
            }
        }

        return start;
    }

    private static int SpanEnd(PdfContentCommand command) =>
        (int)command.SourceSpan.Offset + command.SourceSpan.Length;

    private static IReadOnlyList<double> ReadNumbers(IReadOnlyList<PdfObject> operands)
    {
        var numbers = new List<double>(operands.Count);
        foreach (var operand in operands)
        {
            switch (operand)
            {
                case PdfInteger integer:
                    numbers.Add(integer.Value);
                    break;
                case PdfNumber number:
                    numbers.Add(number.Value);
                    break;
            }
        }

        return numbers;
    }

    private static string Format(double[] rgb, string op) => string.Format(
        CultureInfo.InvariantCulture,
        "{0:0.#####} {1:0.#####} {2:0.#####} {3}",
        Math.Clamp(rgb[0], 0d, 1d),
        Math.Clamp(rgb[1], 0d, 1d),
        Math.Clamp(rgb[2], 0d, 1d),
        op);

    private sealed record ColorGraphicsState(
        PdfResolvedColorSpace? FillSpace,
        PdfResolvedColorSpace? StrokeSpace,
        string? FillSpaceName,
        string? StrokeSpaceName);
}
