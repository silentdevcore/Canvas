using Canvas.Importer.Content;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Graphics;

public sealed class PdfGraphicsInterpreter
{
    public IReadOnlyList<PdfGraphicsElement> Interpret(IReadOnlyList<PdfContentCommand> commands)
    {
        var state = new GraphicsStateStack();
        var elements = new List<PdfGraphicsElement>();
        var groups = new Stack<PdfGroupElement>();
        var path = new List<PdfPathSegment>();

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
                    state.Update(s => s with { FontSize = Number(command.Operands, 1) });
                    break;
                case "Tc":
                    state.Update(s => s with { CharacterSpacing = Number(command.Operands, 0) });
                    break;
                case "Tw":
                    state.Update(s => s with { WordSpacing = Number(command.Operands, 0) });
                    break;
                case "TL":
                    state.Update(s => s with { TextLeading = Number(command.Operands, 0) });
                    break;
                case "Tm":
                    state.Update(s => s with { TextMatrix = ReadMatrix(command.Operands), TextLineMatrix = ReadMatrix(command.Operands) });
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
                case "m":
                    path.Add(new MoveToSegment(new PdfPoint(Number(command.Operands, 0), Number(command.Operands, 1))));
                    break;
                case "l":
                    path.Add(new LineToSegment(new PdfPoint(Number(command.Operands, 0), Number(command.Operands, 1))));
                    break;
                case "c":
                    path.Add(new CurveToSegment(
                        new PdfPoint(Number(command.Operands, 0), Number(command.Operands, 1)),
                        new PdfPoint(Number(command.Operands, 2), Number(command.Operands, 3)),
                        new PdfPoint(Number(command.Operands, 4), Number(command.Operands, 5))));
                    break;
                case "h":
                    path.Add(new ClosePathSegment());
                    break;
                case "re":
                    path.Add(new RectangleSegment(new PdfRectangle(Number(command.Operands, 0), Number(command.Operands, 1), Number(command.Operands, 2), Number(command.Operands, 3))));
                    break;
                case "S" or "s" or "f" or "F" or "f*" or "B" or "B*" or "b" or "b*":
                    AddElement(groups, elements, new PdfPathElement(command.Sequence, state.Current.Transform, command, path)
                    {
                        FillColor = state.Current.FillColor,
                        StrokeColor = state.Current.StrokeColor,
                        LineWidth = state.Current.LineWidth
                    });
                    path = [];
                    break;
                case "n":
                    path = [];
                    break;
                case "Tj" or "'" or "\"":
                    AddElement(groups, elements, new PdfTextElement(command.Sequence, state.Current.TextMatrix.Multiply(state.Current.Transform), command, Text(command.Operands[^1]))
                    {
                        FontSize = state.Current.FontSize,
                        FillColor = state.Current.FillColor,
                        StrokeColor = state.Current.StrokeColor
                    });
                    break;
                case "TJ":
                    AddElement(groups, elements, new PdfTextElement(command.Sequence, state.Current.TextMatrix.Multiply(state.Current.Transform), command, TextArray(command.Operands.FirstOrDefault()))
                    {
                        FontSize = state.Current.FontSize,
                        FillColor = state.Current.FillColor,
                        StrokeColor = state.Current.StrokeColor
                    });
                    break;
                case "Do":
                    AddElement(groups, elements, new PdfImageElement(command.Sequence, state.Current.Transform, command, Name(command.Operands, 0)));
                    break;
                case "BI":
                    if (command.Operands.FirstOrDefault() is PdfStreamObject inlineImage)
                    {
                        AddElement(groups, elements, new PdfImageElement(command.Sequence, state.Current.Transform, command, string.Empty)
                        {
                            ImageBytes = inlineImage.EncodedBytes
                        });
                    }

                    break;
            }
        }

        return elements;
    }

    private static PdfMatrix ReadMatrix(IReadOnlyList<PdfObject> operands)
    {
        return new PdfMatrix(Number(operands, 0), Number(operands, 1), Number(operands, 2), Number(operands, 3), Number(operands, 4), Number(operands, 5));
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

    private static string Text(PdfObject operand)
    {
        return operand is PdfString text ? text.ToLatin1String() : string.Empty;
    }

    private static string TextArray(PdfObject? operand)
    {
        if (operand is not PdfArray array)
        {
            return string.Empty;
        }

        return string.Concat(array.Items.OfType<PdfString>().Select(item => item.ToLatin1String()));
    }
}
