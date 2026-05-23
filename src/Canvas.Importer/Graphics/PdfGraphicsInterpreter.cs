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
                    state.Update(s => s with { StrokeColor = GrayColor(command.Operands) });
                    break;
                case "g":
                    state.Update(s => s with { FillColor = GrayColor(command.Operands) });
                    break;
                case "RG":
                    state.Update(s => s with { StrokeColor = RgbColor(command.Operands) });
                    break;
                case "rg":
                    state.Update(s => s with { FillColor = RgbColor(command.Operands) });
                    break;
                case "SC":
                    state.Update(s => s with { StrokeColor = GeneralColor(command.Operands, s.StrokeColor) });
                    break;
                case "sc":
                    state.Update(s => s with { FillColor = GeneralColor(command.Operands, s.FillColor) });
                    break;
                case "K":
                    state.Update(s => s with { StrokeColor = CmykColor(command.Operands) });
                    break;
                case "k":
                    state.Update(s => s with { FillColor = CmykColor(command.Operands) });
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

    private static PdfColor GeneralColor(IReadOnlyList<PdfObject> operands, PdfColor current)
    {
        return NumericComponentCount(operands) switch
        {
            1 => GrayColor(operands),
            3 => RgbColor(operands),
            4 => CmykColor(operands),
            _ => current
        };
    }

    private static int NumericComponentCount(IReadOnlyList<PdfObject> operands)
    {
        var count = 0;
        foreach (var operand in operands)
        {
            if (operand is not (PdfInteger or PdfNumber))
            {
                break;
            }

            count++;
        }

        return count;
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
