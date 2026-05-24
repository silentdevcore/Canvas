using Canvas.Importer.Content;
using Canvas.Importer.Fonts;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Graphics;

public sealed class PdfGraphicsInterpreter
{
    public IReadOnlyList<PdfGraphicsElement> Interpret(IReadOnlyList<PdfContentCommand> commands)
    {
        return Interpret(commands, null);
    }

    public IReadOnlyList<PdfGraphicsElement> Interpret(IReadOnlyList<PdfContentCommand> commands, IReadOnlyDictionary<string, PdfFontResource>? fontResources)
    {
        var state = new GraphicsStateStack();
        var elements = new List<PdfGraphicsElement>();
        var groups = new Stack<PdfGroupElement>();
        var path = new List<PdfPathSegment>();
        var currentPoint = new PdfPoint(0, 0);
        var subpathStart = new PdfPoint(0, 0);

        foreach (var command in commands)
        {
            switch (command.Operator.Name)
            {
                case "q":
                    state.Save();
                    break;
                case "Q":
                    state.Restore();
                    break;
                case "cm":
                    state.Update(s => s with { Transform = s.Transform.Multiply(ReadMatrix(command.Operands)) });
                    break;
                case "w":
                    state.Update(s => s with { LineWidth = Number(command.Operands, 0) });
                    break;
                case "G":
                    state.Update(s => s with { StrokeColorSpace = PdfColorSpace.DeviceGray, StrokeColor = GrayColor(command.Operands) });
                    break;
                case "g":
                    state.Update(s => s with { FillColorSpace = PdfColorSpace.DeviceGray, FillColor = GrayColor(command.Operands) });
                    break;
                case "RG":
                    state.Update(s => s with { StrokeColorSpace = PdfColorSpace.DeviceRgb, StrokeColor = RgbColor(command.Operands) });
                    break;
                case "rg":
                    state.Update(s => s with { FillColorSpace = PdfColorSpace.DeviceRgb, FillColor = RgbColor(command.Operands) });
                    break;
                case "CS":
                    state.Update(s => s with { StrokeColorSpace = ReadColorSpace(command.Operands) });
                    break;
                case "cs":
                    state.Update(s => s with { FillColorSpace = ReadColorSpace(command.Operands) });
                    break;
                case "SC":
                    state.Update(s => s with { StrokeColor = GeneralColor(command.Operands, s.StrokeColorSpace, s.StrokeColor) });
                    break;
                case "SCN":
                    state.Update(s => s with { StrokeColor = GeneralColor(command.Operands, s.StrokeColorSpace, s.StrokeColor) });
                    break;
                case "sc":
                    state.Update(s => s with { FillColor = GeneralColor(command.Operands, s.FillColorSpace, s.FillColor) });
                    break;
                case "scn":
                    state.Update(s => s with { FillColor = GeneralColor(command.Operands, s.FillColorSpace, s.FillColor) });
                    break;
                case "K":
                    state.Update(s => s with { StrokeColorSpace = PdfColorSpace.DeviceCmyk, StrokeColor = CmykColor(command.Operands) });
                    break;
                case "k":
                    state.Update(s => s with { FillColorSpace = PdfColorSpace.DeviceCmyk, FillColor = CmykColor(command.Operands) });
                    break;
                case "Tf":
                    var fontName = Name(command.Operands, 0);
                    fontResources ??= new Dictionary<string, PdfFontResource>();
                    fontResources.TryGetValue(fontName, out var font);
                    state.Update(s => s with { CurrentFont = font, CurrentFontResourceName = fontName, FontSize = Number(command.Operands, 1) });
                    break;
                case "Tc":
                    state.Update(s => s with { CharacterSpacing = Number(command.Operands, 0) });
                    break;
                case "Tw":
                    state.Update(s => s with { WordSpacing = Number(command.Operands, 0) });
                    break;
                case "Tz":
                    state.Update(s => s with { HorizontalScaling = Number(command.Operands, 0) / 100d });
                    break;
                case "BT":
                    state.Update(s => s with { TextMatrix = PdfMatrix.Identity, TextLineMatrix = PdfMatrix.Identity });
                    break;
                case "ET":
                    state.Update(s => s with { TextMatrix = PdfMatrix.Identity, TextLineMatrix = PdfMatrix.Identity });
                    break;
                case "TL":
                    state.Update(s => s with { TextLeading = Number(command.Operands, 0) });
                    break;
                case "Td":
                    UpdateTextPosition(state, Number(command.Operands, 0), Number(command.Operands, 1));
                    break;
                case "TD":
                    state.Update(s => s with { TextLeading = -Number(command.Operands, 1) });
                    UpdateTextPosition(state, Number(command.Operands, 0), Number(command.Operands, 1));
                    break;
                case "Tm":
                    state.Update(s => s with { TextMatrix = ReadMatrix(command.Operands), TextLineMatrix = ReadMatrix(command.Operands) });
                    break;
                case "T*":
                    MoveToNextTextLine(state);
                    break;
                case "BMC":
                    BeginMarkedContentGroup(command, state.Current.Transform, groups, elements, hasProperties: false);
                    break;
                case "BDC":
                    BeginMarkedContentGroup(command, state.Current.Transform, groups, elements, hasProperties: true);
                    break;
                case "MP":
                    AddMarkedContentMarker(command, state.Current.Transform, groups, elements, hasProperties: false);
                    break;
                case "DP":
                    AddMarkedContentMarker(command, state.Current.Transform, groups, elements, hasProperties: true);
                    break;
                case "EMC":
                    if (groups.Count > 0)
                    {
                        groups.Pop();
                    }

                    break;
                case "BX":
                    BeginCompatibilityGroup(command, state.Current, groups, elements);
                    break;
                case "EX":
                    if (groups.Count > 0 && groups.Peek().IsCompatibilitySection)
                    {
                        groups.Pop();
                    }

                    break;
                case "m":
                    currentPoint = new PdfPoint(Number(command.Operands, 0), Number(command.Operands, 1));
                    subpathStart = currentPoint;
                    path.Add(new MoveToSegment(currentPoint));
                    break;
                case "l":
                    currentPoint = new PdfPoint(Number(command.Operands, 0), Number(command.Operands, 1));
                    path.Add(new LineToSegment(currentPoint));
                    break;
                case "c":
                    currentPoint = new PdfPoint(Number(command.Operands, 4), Number(command.Operands, 5));
                    path.Add(new CurveToSegment(
                        new PdfPoint(Number(command.Operands, 0), Number(command.Operands, 1)),
                        new PdfPoint(Number(command.Operands, 2), Number(command.Operands, 3)),
                        currentPoint));
                    break;
                case "v":
                    var vEnd = new PdfPoint(Number(command.Operands, 2), Number(command.Operands, 3));
                    path.Add(new CurveToSegment(
                        currentPoint,
                        new PdfPoint(Number(command.Operands, 0), Number(command.Operands, 1)),
                        vEnd));
                    currentPoint = vEnd;
                    break;
                case "y":
                    var yEnd = new PdfPoint(Number(command.Operands, 2), Number(command.Operands, 3));
                    path.Add(new CurveToSegment(
                        new PdfPoint(Number(command.Operands, 0), Number(command.Operands, 1)),
                        yEnd,
                        yEnd));
                    currentPoint = yEnd;
                    break;
                case "h":
                    path.Add(new ClosePathSegment());
                    currentPoint = subpathStart;
                    break;
                case "re":
                    path.Add(new RectangleSegment(new PdfRectangle(Number(command.Operands, 0), Number(command.Operands, 1), Number(command.Operands, 2), Number(command.Operands, 3))));
                    break;
                case "W":
                    state.Update(s => s with { PendingClippingPath = CreateClippingPath(path, usesEvenOddRule: false) });
                    break;
                case "W*":
                    state.Update(s => s with { PendingClippingPath = CreateClippingPath(path, usesEvenOddRule: true) });
                    break;
                case "S" or "s" or "f" or "F" or "f*" or "B" or "B*" or "b" or "b*":
                    CommitPendingClippingPath(state);
                    if (command.Operator.Name is "s" or "b" or "b*")
                    {
                        path.Add(new ClosePathSegment());
                    }

                    AddElement(groups, elements, new PdfPathElement(command.Sequence, state.Current.Transform, command, path)
                    {
                        FillColor = state.Current.FillColor,
                        StrokeColor = state.Current.StrokeColor,
                        LineWidth = state.Current.LineWidth,
                        ClippingPath = state.Current.ClippingPath
                    });
                    path = [];
                    currentPoint = new PdfPoint(0, 0);
                    subpathStart = currentPoint;
                    break;
                case "n":
                    CommitPendingClippingPath(state);
                    path = [];
                    currentPoint = new PdfPoint(0, 0);
                    subpathStart = currentPoint;
                    break;
                case "Tj":
                    var textOperand = command.Operands[^1];
                    AddTextElement(command, Text(textOperand, state.Current.CurrentFont), state.Current, groups, elements);
                    AdvanceTextMatrix(state, ComputeTextAdvance(textOperand, state.Current));
                    break;
                case "'":
                    MoveToNextTextLine(state);
                    var nextLineTextOperand = command.Operands[^1];
                    AddTextElement(command, Text(nextLineTextOperand, state.Current.CurrentFont), state.Current, groups, elements);
                    AdvanceTextMatrix(state, ComputeTextAdvance(nextLineTextOperand, state.Current));
                    break;
                case "\"":
                    state.Update(s => s with { WordSpacing = Number(command.Operands, 0), CharacterSpacing = Number(command.Operands, 1) });
                    MoveToNextTextLine(state);
                    var quoteTextOperand = command.Operands[^1];
                    AddTextElement(command, Text(quoteTextOperand, state.Current.CurrentFont), state.Current, groups, elements);
                    AdvanceTextMatrix(state, ComputeTextAdvance(quoteTextOperand, state.Current));
                    break;
                case "TJ":
                    var arrayOperand = command.Operands.FirstOrDefault();
                    AddTextElement(command, TextArray(arrayOperand, state.Current.CurrentFont), state.Current, groups, elements);
                    AdvanceTextMatrix(state, ComputeTextAdvance(arrayOperand, state.Current));
                    break;
                case "Do":
                    AddElement(groups, elements, new PdfImageElement(command.Sequence, state.Current.Transform, command, Name(command.Operands, 0))
                    {
                        ClippingPath = state.Current.ClippingPath
                    });
                    break;
                case "BI":
                    if (command.Operands.FirstOrDefault() is PdfStreamObject inlineImage)
                    {
                        AddElement(groups, elements, new PdfImageElement(command.Sequence, state.Current.Transform, command, string.Empty)
                        {
                            ImageBytes = inlineImage.EncodedBytes,
                            ClippingPath = state.Current.ClippingPath
                        });
                    }

                    break;
                case "sh":
                    AddElement(groups, elements, new PdfShadingElement(command.Sequence, state.Current.Transform, command, Name(command.Operands, 0))
                    {
                        ClippingPath = state.Current.ClippingPath
                    });
                    break;
            }
        }

        return elements;
    }

    private static PdfMatrix ReadMatrix(IReadOnlyList<PdfObject> operands)
    {
        return new PdfMatrix(Number(operands, 0), Number(operands, 1), Number(operands, 2), Number(operands, 3), Number(operands, 4), Number(operands, 5));
    }

    private static void AddTextElement(
        PdfContentCommand command,
        string text,
        GraphicsState state,
        Stack<PdfGroupElement> groups,
        List<PdfGraphicsElement> elements)
    {
        var composed = state.TextMatrix.Multiply(state.Transform);
        var baseFontName = state.CurrentFont?.BaseFontName;
        AddElement(groups, elements, new PdfTextElement(command.Sequence, composed, command, text)
        {
            FontSize = state.FontSize,
            FontResourceName = state.CurrentFontResourceName,
            FontName = baseFontName,
            Bold = state.CurrentFont?.Bold ?? IsBold(baseFontName),
            Italic = state.CurrentFont?.Italic ?? IsItalic(baseFontName),
            EmbeddedFontBytes = state.CurrentFont?.EmbeddedFontBytes ?? ReadOnlyMemory<byte>.Empty,
            EmbeddedFontFormat = state.CurrentFont?.EmbeddedFontFormat,
            EmbeddedFontMimeType = state.CurrentFont?.EmbeddedFontMimeType,
            UsesToUnicodeMap = state.CurrentFont?.ToUnicode is not null,
            IsSubsetFont = state.CurrentFont?.IsSubset ?? false,
            FillColor = state.FillColor,
            StrokeColor = state.StrokeColor,
            ClippingPath = state.ClippingPath
        });
    }

    private static void AdvanceTextMatrix(GraphicsStateStack state, double tx)
    {
        if (tx == 0)
        {
            return;
        }

        state.Update(s => s with { TextMatrix = s.TextMatrix.Multiply(new PdfMatrix(1, 0, 0, 1, tx, 0)) });
    }

    private static void UpdateTextPosition(GraphicsStateStack state, double tx, double ty)
    {
        var translate = new PdfMatrix(1, 0, 0, 1, tx, ty);
        state.Update(s =>
        {
            var nextLineMatrix = s.TextLineMatrix.Multiply(translate);
            return s with
            {
                TextLineMatrix = nextLineMatrix,
                TextMatrix = nextLineMatrix
            };
        });
    }

    private static void MoveToNextTextLine(GraphicsStateStack state)
    {
        UpdateTextPosition(state, 0, -state.Current.TextLeading);
    }

    private static double ComputeTextAdvance(PdfObject? operand, GraphicsState state)
    {
        return operand switch
        {
            PdfString text => ComputeStringAdvance(text.GetDecodedBytes().Span, state),
            PdfArray array => ComputeArrayAdvance(array, state),
            _ => 0
        };
    }

    private static double ComputeArrayAdvance(PdfArray array, GraphicsState state)
    {
        var advance = 0d;
        foreach (var item in array.Items)
        {
            switch (item)
            {
                case PdfString text:
                    advance += ComputeStringAdvance(text.GetDecodedBytes().Span, state);
                    break;
                case PdfInteger integer:
                    advance -= (integer.Value / 1000d) * state.FontSize * state.HorizontalScaling;
                    break;
                case PdfNumber number:
                    advance -= (number.Value / 1000d) * state.FontSize * state.HorizontalScaling;
                    break;
            }
        }

        return advance;
    }

    private static double ComputeStringAdvance(ReadOnlySpan<byte> glyphBytes, GraphicsState state)
    {
        if (glyphBytes.Length == 0)
        {
            return 0;
        }

        var glyphAdvance = 0d;
        var font = state.CurrentFont;
        foreach (var glyph in font?.GetGlyphCodes(glyphBytes) ?? glyphBytes.ToArray().Select(static value => (int)value))
        {
            glyphAdvance += font?.GetGlyphWidth(glyph) ?? 0;
            glyphAdvance += state.CharacterSpacing * 1000d / Math.Max(state.FontSize, 1);
            if (glyph == (byte)' ')
            {
                glyphAdvance += state.WordSpacing * 1000d / Math.Max(state.FontSize, 1);
            }
        }

        return (glyphAdvance / 1000d) * state.FontSize * state.HorizontalScaling;
    }

    private static PdfClippingPath? CreateClippingPath(IReadOnlyList<PdfPathSegment> path, bool usesEvenOddRule)
    {
        return path.Count == 0 ? null : new PdfClippingPath([.. path], usesEvenOddRule);
    }

    private static void CommitPendingClippingPath(GraphicsStateStack state)
    {
        if (state.Current.PendingClippingPath is null)
        {
            return;
        }

        state.Update(s => s with
        {
            ClippingPath = s.PendingClippingPath,
            PendingClippingPath = null
        });
    }

    private static void BeginMarkedContentGroup(
        PdfContentCommand command,
        PdfMatrix transform,
        Stack<PdfGroupElement> groups,
        List<PdfGraphicsElement> elements,
        bool hasProperties)
    {
        var group = new PdfGroupElement(command.Sequence, transform, command)
        {
            MarkedContentTag = Name(command.Operands, 0),
            Properties = hasProperties && command.Operands.Count > 1 ? command.Operands[1] : null
        };

        AddElement(groups, elements, group);
        groups.Push(group);
    }

    private static void BeginCompatibilityGroup(
        PdfContentCommand command,
        GraphicsState state,
        Stack<PdfGroupElement> groups,
        List<PdfGraphicsElement> elements)
    {
        var group = new PdfGroupElement(command.Sequence, state.Transform, command)
        {
            IsCompatibilitySection = true,
            ClippingPath = state.ClippingPath
        };

        AddElement(groups, elements, group);
        groups.Push(group);
    }

    private static void AddMarkedContentMarker(
        PdfContentCommand command,
        PdfMatrix transform,
        Stack<PdfGroupElement> groups,
        List<PdfGraphicsElement> elements,
        bool hasProperties)
    {
        AddElement(groups, elements, new PdfGroupElement(command.Sequence, transform, command)
        {
            MarkedContentTag = Name(command.Operands, 0),
            Properties = hasProperties && command.Operands.Count > 1 ? command.Operands[1] : null
        });
    }

    private static void AddElement(Stack<PdfGroupElement> groups, List<PdfGraphicsElement> elements, PdfGraphicsElement element)
    {
        if (groups.Count > 0)
        {
            groups.Peek().Children.Add(element);
            return;
        }

        elements.Add(element);
    }

    private static PdfColor GrayColor(IReadOnlyList<PdfObject> operands)
    {
        return new PdfColor(Number(operands, 0), 0, 0, 1, PdfColorSpace.DeviceGray);
    }

    private static PdfColor RgbColor(IReadOnlyList<PdfObject> operands)
    {
        return new PdfColor(Number(operands, 0), Number(operands, 1), Number(operands, 2), 1, PdfColorSpace.DeviceRgb);
    }

    private static PdfColor CmykColor(IReadOnlyList<PdfObject> operands)
    {
        return new PdfColor(Number(operands, 0), Number(operands, 1), Number(operands, 2), Number(operands, 3), PdfColorSpace.DeviceCmyk);
    }

    private static PdfColorSpace ReadColorSpace(IReadOnlyList<PdfObject> operands)
    {
        return Name(operands, 0) switch
        {
            "DeviceGray" or "G" => PdfColorSpace.DeviceGray,
            "DeviceRGB" or "RGB" => PdfColorSpace.DeviceRgb,
            "DeviceCMYK" or "CMYK" => PdfColorSpace.DeviceCmyk,
            "Pattern" => PdfColorSpace.Pattern,
            "Indexed" => PdfColorSpace.Indexed,
            "ICCBased" => PdfColorSpace.IccBased,
            "Separation" => PdfColorSpace.Separation,
            "DeviceN" => PdfColorSpace.DeviceN,
            _ => PdfColorSpace.DeviceGray
        };
    }

    private static PdfColor GeneralColor(IReadOnlyList<PdfObject> operands, PdfColorSpace colorSpace, PdfColor current)
    {
        var components = NumericComponents(operands);
        if (components.Count == 0)
        {
            return current;
        }

        colorSpace = ResolveDeviceColorSpace(colorSpace, components.Count);

        return colorSpace switch
        {
            PdfColorSpace.DeviceGray => new PdfColor(components[0], 0, 0, 1, PdfColorSpace.DeviceGray),
            PdfColorSpace.DeviceRgb => new PdfColor(Component(components, 0), Component(components, 1), Component(components, 2), 1, PdfColorSpace.DeviceRgb),
            PdfColorSpace.DeviceCmyk => new PdfColor(Component(components, 0), Component(components, 1), Component(components, 2), Component(components, 3), PdfColorSpace.DeviceCmyk),
            PdfColorSpace.Pattern => new PdfColor(Component(components, 0), Component(components, 1), Component(components, 2), Component(components, 3), PdfColorSpace.Pattern),
            PdfColorSpace.Indexed => new PdfColor(Component(components, 0), 0, 0, 1, PdfColorSpace.Indexed),
            PdfColorSpace.IccBased => new PdfColor(Component(components, 0), Component(components, 1), Component(components, 2), Component(components, 3), PdfColorSpace.IccBased),
            PdfColorSpace.Separation => new PdfColor(Component(components, 0), 0, 0, 1, PdfColorSpace.Separation),
            PdfColorSpace.DeviceN => new PdfColor(Component(components, 0), Component(components, 1), Component(components, 2), Component(components, 3), PdfColorSpace.DeviceN),
            _ => current
        };
    }

    private static PdfColorSpace ResolveDeviceColorSpace(PdfColorSpace colorSpace, int componentCount)
    {
        return componentCount switch
        {
            1 when colorSpace is PdfColorSpace.DeviceGray or PdfColorSpace.DeviceRgb or PdfColorSpace.DeviceCmyk => PdfColorSpace.DeviceGray,
            3 when colorSpace is PdfColorSpace.DeviceGray or PdfColorSpace.DeviceRgb or PdfColorSpace.DeviceCmyk => PdfColorSpace.DeviceRgb,
            4 when colorSpace is PdfColorSpace.DeviceGray or PdfColorSpace.DeviceRgb or PdfColorSpace.DeviceCmyk => PdfColorSpace.DeviceCmyk,
            _ => colorSpace
        };
    }

    private static List<double> NumericComponents(IReadOnlyList<PdfObject> operands)
    {
        var components = new List<double>();
        foreach (var operand in operands)
        {
            switch (operand)
            {
                case PdfInteger integer:
                    components.Add(integer.Value);
                    break;
                case PdfNumber number:
                    components.Add(number.Value);
                    break;
                default:
                    return components;
            }
        }

        return components;
    }

    private static double Component(IReadOnlyList<double> components, int index)
    {
        return index < components.Count ? components[index] : 0;
    }

    private static double Number(IReadOnlyList<PdfObject> operands, int index)
    {
        if (index >= operands.Count)
        {
            return 0;
        }

        return operands[index] switch
        {
            PdfInteger integer => integer.Value,
            PdfNumber number => number.Value,
            _ => 0
        };
    }

    private static string Name(IReadOnlyList<PdfObject> operands, int index)
    {
        return index < operands.Count && operands[index] is PdfName name ? name.Value : string.Empty;
    }

    private static string Text(PdfObject operand, PdfFontResource? font)
    {
        return operand is PdfString text ? (font?.Decode(text.GetDecodedBytes().Span) ?? text.ToLatin1String()) : string.Empty;
    }

    private static string TextArray(PdfObject? operand, PdfFontResource? font)
    {
        if (operand is not PdfArray array)
        {
            return string.Empty;
        }

        return string.Concat(array.Items.OfType<PdfString>().Select(item => font?.Decode(item.GetDecodedBytes().Span) ?? item.ToLatin1String()));
    }

    private static bool IsBold(string? baseFontName) =>
        baseFontName is not null &&
        (baseFontName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ||
         baseFontName.Contains("-Bd", StringComparison.OrdinalIgnoreCase) ||
         baseFontName.Contains("SemiBold", StringComparison.OrdinalIgnoreCase) ||
         baseFontName.Contains("Semibold", StringComparison.OrdinalIgnoreCase) ||
         baseFontName.Contains("Demi", StringComparison.OrdinalIgnoreCase) ||
         baseFontName.Contains("Heavy", StringComparison.OrdinalIgnoreCase) ||
         baseFontName.Contains("Black", StringComparison.OrdinalIgnoreCase));

    private static bool IsItalic(string? baseFontName) =>
        baseFontName is not null &&
        (baseFontName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
         baseFontName.Contains("-It", StringComparison.OrdinalIgnoreCase) ||
         baseFontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase));
}
