using System.Globalization;
using System.Text;
using Canvas.Importer.Document;
using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;

namespace Canvas.Importer.Content;

public sealed class PdfContentStreamRewriter
{
    public ReadOnlyMemory<byte> Rewrite(PdfPageModel page)
    {
        return Rewrite(page.GraphicsObjects);
    }

    public ReadOnlyMemory<byte> Rewrite(IEnumerable<PdfGraphicsElement> elements)
    {
        using var stream = new MemoryStream();
        foreach (var element in elements.Where(element => !element.IsDeleted).OrderBy(element => element.ZOrder))
        {
            WriteElement(stream, element);
        }

        return stream.ToArray();
    }

    private static void WriteElement(Stream stream, PdfGraphicsElement element)
    {
        switch (element)
        {
            case PdfTextElement text:
                WriteTextElement(stream, text);
                break;
            case PdfPathElement path:
                WriteWrappedGraphicsElement(stream, path.Transform, path.ClippingPath, wrappedStream =>
                {
                    WriteColor(wrappedStream, path.FillColor, stroke: false);
                    WriteColor(wrappedStream, path.StrokeColor, stroke: true);
                    WriteNumberOperator(wrappedStream, path.LineWidth, "w");
                    WritePathSegments(wrappedStream, path.Segments);
                    WriteOperator(wrappedStream, path.SourceCommand.Operator.Name);
                });
                break;
            case PdfImageElement image:
                WriteWrappedGraphicsElement(stream, image.Transform, image.ClippingPath, wrappedStream =>
                {
                    if (!string.IsNullOrEmpty(image.ResourceName))
                    {
                        WriteName(wrappedStream, image.ResourceName);
                        WriteOperator(wrappedStream, "Do");
                        return;
                    }

                    if (image.SourceCommand.Operands.FirstOrDefault() is PdfStreamObject inlineImage)
                    {
                        WriteInlineImage(wrappedStream, inlineImage);
                    }
                });
                break;
            case PdfShadingElement shading:
                WriteWrappedGraphicsElement(stream, shading.Transform, shading.ClippingPath, wrappedStream =>
                {
                    WriteName(wrappedStream, shading.ResourceName);
                    WriteOperator(wrappedStream, "sh");
                });
                break;
            case PdfGroupElement group:
                WriteGroup(stream, group);
                break;
        }
    }

    private static void WriteTextElement(Stream stream, PdfTextElement text)
    {
        WriteAscii(stream, "BT\n");
        if (!string.IsNullOrEmpty(text.FontResourceName))
        {
            WriteName(stream, text.FontResourceName);
            WriteAscii(stream, " ");
            WriteNumber(stream, text.FontSize);
            WriteOperator(stream, "Tf");
        }

        WriteColor(stream, text.FillColor, stroke: false);
        WriteColor(stream, text.StrokeColor, stroke: true);
        WriteMatrixOperator(stream, text.Transform, "Tm");
        WriteString(stream, text.Text);
        WriteOperator(stream, "Tj");
        WriteAscii(stream, "ET\n");
    }

    private static void WriteGroup(Stream stream, PdfGroupElement group)
    {
        if (group.IsCompatibilitySection)
        {
            WriteOperator(stream, "BX");
            foreach (var child in group.Children.Where(child => !child.IsDeleted).OrderBy(child => child.ZOrder))
            {
                WriteElement(stream, child);
            }

            WriteOperator(stream, "EX");
            return;
        }

        if (!string.IsNullOrEmpty(group.MarkedContentTag))
        {
            WriteName(stream, group.MarkedContentTag);
            if (group.Children.Count == 0)
            {
                if (group.Properties is not null)
                {
                    WriteAscii(stream, " ");
                    WriteObject(stream, group.Properties);
                    WriteOperator(stream, "DP");
                }
                else
                {
                    WriteOperator(stream, "MP");
                }

                return;
            }

            if (group.Properties is not null)
            {
                WriteAscii(stream, " ");
                WriteObject(stream, group.Properties);
                WriteOperator(stream, "BDC");
            }
            else
            {
                WriteOperator(stream, "BMC");
            }

            foreach (var child in group.Children.Where(child => !child.IsDeleted).OrderBy(child => child.ZOrder))
            {
                WriteElement(stream, child);
            }

            WriteOperator(stream, "EMC");
            return;
        }

        foreach (var child in group.Children.Where(child => !child.IsDeleted).OrderBy(child => child.ZOrder))
        {
            WriteElement(stream, child);
        }
    }

    private static void WriteWrappedGraphicsElement(Stream stream, PdfMatrix transform, PdfClippingPath? clippingPath, Action<Stream> writer)
    {
        WriteOperator(stream, "q");
        WriteMatrixOperator(stream, transform, "cm");
        if (clippingPath is not null)
        {
            WritePathSegments(stream, clippingPath.Segments);
            WriteOperator(stream, clippingPath.UsesEvenOddRule ? "W*" : "W");
            WriteOperator(stream, "n");
        }

        writer(stream);
        WriteOperator(stream, "Q");
    }

    private static void WriteInlineImage(Stream stream, PdfStreamObject inlineImage)
    {
        WriteAscii(stream, "BI\n");
        foreach (var entry in inlineImage.Dictionary.Values)
        {
            WriteName(stream, entry.Key);
            WriteAscii(stream, " ");
            WriteObject(stream, entry.Value);
            WriteAscii(stream, "\n");
        }

        WriteAscii(stream, "ID\n");
        stream.Write(inlineImage.EncodedBytes.Span);
        WriteAscii(stream, "\nEI\n");
    }

    private static void WritePathSegments(Stream stream, IReadOnlyList<PdfPathSegment> segments)
    {
        foreach (var segment in segments)
        {
            switch (segment)
            {
                case MoveToSegment moveTo:
                    WritePointOperator(stream, moveTo.Point, "m");
                    break;
                case LineToSegment lineTo:
                    WritePointOperator(stream, lineTo.Point, "l");
                    break;
                case CurveToSegment curveTo:
                    WriteNumber(stream, curveTo.Control1.X);
                    WriteAscii(stream, " ");
                    WriteNumber(stream, curveTo.Control1.Y);
                    WriteAscii(stream, " ");
                    WriteNumber(stream, curveTo.Control2.X);
                    WriteAscii(stream, " ");
                    WriteNumber(stream, curveTo.Control2.Y);
                    WriteAscii(stream, " ");
                    WriteNumber(stream, curveTo.End.X);
                    WriteAscii(stream, " ");
                    WriteNumber(stream, curveTo.End.Y);
                    WriteOperator(stream, "c");
                    break;
                case ClosePathSegment:
                    WriteOperator(stream, "h");
                    break;
                case RectangleSegment rectangle:
                    WriteNumber(stream, rectangle.Rectangle.X);
                    WriteAscii(stream, " ");
                    WriteNumber(stream, rectangle.Rectangle.Y);
                    WriteAscii(stream, " ");
                    WriteNumber(stream, rectangle.Rectangle.Width);
                    WriteAscii(stream, " ");
                    WriteNumber(stream, rectangle.Rectangle.Height);
                    WriteOperator(stream, "re");
                    break;
            }
        }
    }

    private static void WriteColor(Stream stream, PdfColor color, bool stroke)
    {
        switch (color.ColorSpace)
        {
            case PdfColorSpace.DeviceGray:
                WriteNumberOperator(stream, color.C1, stroke ? "G" : "g");
                break;
            case PdfColorSpace.DeviceRgb:
                WriteNumbersOperator(stream, [color.C1, color.C2, color.C3], stroke ? "RG" : "rg");
                break;
            case PdfColorSpace.DeviceCmyk:
                WriteNumbersOperator(stream, [color.C1, color.C2, color.C3, color.C4], stroke ? "K" : "k");
                break;
            default:
                WriteName(stream, ColorSpaceName(color.ColorSpace));
                WriteOperator(stream, stroke ? "CS" : "cs");
                WriteNumbersOperator(stream, [color.C1, color.C2, color.C3, color.C4], stroke ? "SCN" : "scn");
                break;
        }
    }

    private static string ColorSpaceName(PdfColorSpace colorSpace)
    {
        return colorSpace switch
        {
            PdfColorSpace.Pattern => "Pattern",
            PdfColorSpace.Indexed => "Indexed",
            PdfColorSpace.IccBased => "ICCBased",
            PdfColorSpace.Separation => "Separation",
            PdfColorSpace.DeviceN => "DeviceN",
            _ => "DeviceGray"
        };
    }

    private static void WriteMatrixOperator(Stream stream, PdfMatrix matrix, string operatorName)
    {
        WriteNumbersOperator(stream, [matrix.A, matrix.B, matrix.C, matrix.D, matrix.E, matrix.F], operatorName);
    }

    private static void WritePointOperator(Stream stream, PdfPoint point, string operatorName)
    {
        WriteNumbersOperator(stream, [point.X, point.Y], operatorName);
    }

    private static void WriteNumberOperator(Stream stream, double value, string operatorName)
    {
        WriteNumber(stream, value);
        WriteOperator(stream, operatorName);
    }

    private static void WriteNumbersOperator(Stream stream, IReadOnlyList<double> values, string operatorName)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                WriteAscii(stream, " ");
            }

            WriteNumber(stream, values[index]);
        }

        WriteOperator(stream, operatorName);
    }

    private static void WriteString(Stream stream, string value)
    {
        WriteAscii(stream, "<");
        foreach (var current in GetStringBytes(value))
        {
            WriteAscii(stream, current.ToString("X2", CultureInfo.InvariantCulture));
        }

        WriteAscii(stream, "> ");
    }

    private static byte[] GetStringBytes(string value)
    {
        if (value.All(character => character <= byte.MaxValue))
        {
            return Encoding.Latin1.GetBytes(value);
        }

        var unicodeBytes = Encoding.BigEndianUnicode.GetBytes(value);
        return [0xFE, 0xFF, .. unicodeBytes];
    }

    private static void WriteObject(Stream stream, PdfObject value)
    {
        switch (value)
        {
            case PdfName name:
                WriteName(stream, name.Value);
                break;
            case PdfInteger integer:
                WriteAscii(stream, integer.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case PdfNumber number:
                WriteNumber(stream, number.Value);
                break;
            case PdfString text:
                WriteAscii(stream, "<");
                foreach (var current in text.GetDecodedBytes().Span)
                {
                    WriteAscii(stream, current.ToString("X2", CultureInfo.InvariantCulture));
                }

                WriteAscii(stream, ">");
                break;
            case PdfBoolean boolean:
                WriteAscii(stream, boolean.Value ? "true" : "false");
                break;
            case PdfNull:
                WriteAscii(stream, "null");
                break;
            case PdfReference reference:
                WriteAscii(stream, $"{reference.Id.Number} {reference.Id.Generation} R");
                break;
            case PdfArray array:
                WriteAscii(stream, "[");
                for (var index = 0; index < array.Items.Count; index++)
                {
                    if (index > 0)
                    {
                        WriteAscii(stream, " ");
                    }

                    WriteObject(stream, array.Items[index]);
                }

                WriteAscii(stream, "]");
                break;
            case PdfDictionary dictionary:
                WriteAscii(stream, "<<");
                foreach (var entry in dictionary.Values)
                {
                    WriteAscii(stream, " ");
                    WriteName(stream, entry.Key);
                    WriteAscii(stream, " ");
                    WriteObject(stream, entry.Value);
                }

                WriteAscii(stream, " >>");
                break;
        }
    }

    private static void WriteName(Stream stream, string name)
    {
        WriteAscii(stream, "/");
        WriteAscii(stream, name);
    }

    private static void WriteNumber(Stream stream, double value)
    {
        var text = value.ToString("0.###", CultureInfo.InvariantCulture);
        WriteAscii(stream, text);
    }

    private static void WriteOperator(Stream stream, string operatorName)
    {
        WriteAscii(stream, " ");
        WriteAscii(stream, operatorName);
        WriteAscii(stream, "\n");
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}